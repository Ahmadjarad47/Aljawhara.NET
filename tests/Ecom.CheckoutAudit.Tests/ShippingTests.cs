using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecom.CheckoutAudit.Tests;

public sealed class ShippingTests : IAsyncLifetime
{
    private readonly string _databaseName = $"EcomShipping_{Guid.NewGuid():N}";
    private string ConnectionString => PostgresTestDatabase.ConnectionString(_databaseName);
    private CheckoutApiFactory _factory = null!;
    private HttpClient _client = null!;
    private EcomDbContext CreateContext() => new(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        _factory = new CheckoutApiFactory(ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Migration_preserves_existing_shipping_rules_and_only_admins_can_edit()
    {
        var settings = await ReadAsync();
        Assert.Equal(2m, settings.DefaultFee);
        Assert.Equal(20m, settings.FreeShippingThreshold);
        Assert.Equal(7, settings.Areas.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/shipping", TestContext.Current.CancellationToken)).StatusCode);
        using var user = new HttpRequestMessage(HttpMethod.Put, "/api/admin/shipping") { Content = JsonContent.Create(settings) };
        user.Headers.Add("X-Test-User", "ordinary-user");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(user, TestContext.Current.CancellationToken)).StatusCode);
        using var admin = new HttpRequestMessage(HttpMethod.Get, "/api/admin/shipping");
        admin.Headers.Add("X-Test-User", "shipping-admin");
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(admin, TestContext.Current.CancellationToken)).StatusCode);
        await using var db = CreateContext();
        var shipping = new ShippingService(db);
        Assert.Equal(6m, await shipping.CalculateAsync(19.999m, null, "الخيران", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0m, await shipping.CalculateAsync(20m, null, "الخيران", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3m, await shipping.CalculateAsync(10m, null, "صباح السالم", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(4m, await shipping.CalculateAsync(10m, null, "المطلاع", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Saved_rates_apply_to_checkout_while_existing_orders_and_idempotent_retries_keep_their_price()
    {
        int productId;
        await using (var db = CreateContext())
        {
            var product = new Product { Title = "Shipping test", TitleAr = "اختبار", Description = "Test", DescriptionAr = "اختبار",
                newPrice = 10m, oldPrice = 10m, TotalInStock = 10, IsInStock = true,
                subCategory = new SubCategory { Name = "Test", NameAr = "اختبار", Category = new Category { Name = "Test", NameAr = "اختبار" } } };
            db.Products.Add(product);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            productId = product.Id;
        }
        var initial = await CheckoutAsync(productId, "shipping-before-change");
        var settings = await ReadAsync();
        settings.DefaultFee = 4.125m;
        settings.FreeShippingThreshold = null;
        settings.Areas = [new ShippingAreaRateDto { City = "  مدينة   جديدة ", Fee = 7.375m }];
        var saved = await SaveAsync(settings);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var updated = await ReadAsync();
        Assert.Equal(settings.Version + 1, updated.Version);
        Assert.Equal("مدينة جديدة", Assert.Single(updated.Areas).City);

        var newer = await CheckoutAsync(productId, "shipping-after-change");
        var retry = await CheckoutAsync(productId, "shipping-before-change");
        Assert.Equal(initial, retry);
        await using var verification = CreateContext();
        var oldOrder = await verification.Orders.SingleAsync(o => o.Id == initial, TestContext.Current.CancellationToken);
        var newOrder = await verification.Orders.SingleAsync(o => o.Id == newer, TestContext.Current.CancellationToken);
        Assert.Equal(2m, oldOrder.Shipping);
        Assert.Equal(12m, oldOrder.Total);
        Assert.Equal(7.375m, newOrder.Shipping);
        Assert.Equal(17.375m, newOrder.Total);
        Assert.Equal(newOrder.Total, await verification.Transactions.Where(t => t.OrderId == newer).Select(t => t.Amount).SingleAsync(TestContext.Current.CancellationToken));
        var shipping = new ShippingService(verification);
        Assert.Equal(4.125m, await shipping.CalculateAsync(200m, null, "الخيران", cancellationToken: TestContext.Current.CancellationToken)); // Removed area and disabled offer.
        Assert.Equal(4.125m, await shipping.CalculateAsync(1m, null, null, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0m, await shipping.CalculateAsync(1m, CouponType.FreeShipping, "مدينة جديدة", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Offer_can_be_changed_disabled_and_set_to_zero_without_affecting_free_shipping_coupons()
    {
        var settings = await ReadAsync();
        settings.FreeShippingThreshold = 35.125m;
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(settings)).StatusCode);
        await using var db = CreateContext();
        var shipping = new ShippingService(db);
        Assert.Equal(6m, await shipping.CalculateAsync(35.124m, null, "  الخيران  ", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0m, await shipping.CalculateAsync(35.125m, null, "الخيران", cancellationToken: TestContext.Current.CancellationToken));
        settings = await ReadAsync();
        settings.FreeShippingThreshold = 0;
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(settings)).StatusCode);
        Assert.Equal(0m, await shipping.CalculateAsync(1m, null, null, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("threshold")]
    [InlineData("duplicate")]
    [InlineData("blank")]
    public async Task Invalid_settings_are_rejected_without_partial_changes(string kind)
    {
        var settings = await ReadAsync();
        switch (kind)
        {
            case "negative": settings.DefaultFee = -1m; break;
            case "precision": settings.Areas[0].Fee = 1.0001m; break;
            case "threshold": settings.FreeShippingThreshold = -1m; break;
            case "duplicate": settings.Areas = [new() { City = " صباح  السالم ", Fee = 1 }, new() { City = "صباح السالم", Fee = 2 }]; break;
            case "blank": settings.Areas[0].City = "   "; break;
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(settings)).StatusCode);
        var actual = await ReadAsync();
        Assert.Equal(1, actual.Version);
        Assert.Equal(2m, actual.DefaultFee);
        Assert.Equal(7, actual.Areas.Count);
    }

    [Fact]
    public async Task Stale_admin_save_returns_conflict_and_keeps_latest_rates()
    {
        var first = await ReadAsync();
        var stale = await ReadAsync();
        first.DefaultFee = 8;
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(first)).StatusCode);
        stale.DefaultFee = 9;
        stale.Areas.Clear();
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(stale)).StatusCode);
        var actual = await ReadAsync();
        Assert.Equal(8m, actual.DefaultFee);
        Assert.Equal(7, actual.Areas.Count);
    }

    private async Task<ShippingSettingsDto> ReadAsync()
        => (await _client.GetFromJsonAsync<ShippingSettingsDto>("/api/shipping", TestContext.Current.CancellationToken))!;

    private async Task<HttpResponseMessage> SaveAsync(ShippingSettingsDto settings)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/admin/shipping") { Content = JsonContent.Create(settings) };
        request.Headers.Add("X-Test-User", "shipping-admin");
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<int> CheckoutAsync(int productId, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId, quantity = 1 } },
                shippingAddress = new { fullName = "Test", phone = "12345678", street = "Street", city = "مدينة جديدة" }, paymentMethod = (int)PaymentMethod.Cod })
        };
        request.Headers.Add("Idempotency-Key", key);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken)).GetProperty("orderId").GetInt32();
    }
}
