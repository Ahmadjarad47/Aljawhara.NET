using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecom.CheckoutAudit.Tests;

public sealed class WhatsAppConsentIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"WhatsAppConsent_{Guid.NewGuid():N}";
    private string ConnectionString => PostgresTestDatabase.ConnectionString(_databaseName);
    private CheckoutApiFactory _factory = null!;
    private HttpClient _client = null!;
    private int _productId;
    private const string UserId = "whatsapp-consent-user";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private EcomDbContext Db() => new(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask InitializeAsync()
    {
        await using (var db = Db())
        {
            await db.Database.MigrateAsync(Ct);
            var user = new AppUsers
            {
                Id = UserId, UserName = "whatsapp-user", NormalizedUserName = "WHATSAPP-USER",
                Email = "whatsapp@example.test", NormalizedEmail = "WHATSAPP@EXAMPLE.TEST",
                EmailConfirmed = true, PhoneNumber = "96550000000", PhoneNumberConfirmed = false
            };
            var category = new Category { Name = "Consent", NameAr = "الموافقة" };
            var sub = new SubCategory { Name = "Consent", NameAr = "الموافقة", Category = category };
            var product = new Product
            {
                Title = "Consent product", TitleAr = "منتج", Description = "Test", DescriptionAr = "اختبار",
                subCategory = sub, oldPrice = 4m, newPrice = 4m, TotalInStock = 20, IsInStock = true
            };
            db.AddRange(user, product);
            await db.SaveChangesAsync(Ct);
            _productId = product.Id;
        }
        _factory = new CheckoutApiFactory(ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await using var db = Db();
        await db.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task Checkout_persists_normalized_explicit_consent_and_unchecked_checkout_still_succeeds()
    {
        var optedIn = await CheckoutAsync("consent-opt-in", true, "+965 5000-0000", "00965 5000 0000");
        var optedOut = await CheckoutAsync("consent-opt-out", false, null, "50000000");

        Assert.Equal(HttpStatusCode.Created, optedIn.StatusCode);
        Assert.Equal(HttpStatusCode.Created, optedOut.StatusCode);
        await using var db = Db();
        var orders = await db.Orders.AsNoTracking().OrderBy(o => o.Id).ToListAsync(Ct);
        var consentOrder = orders.Single(o => o.WhatsAppConsentAtUtc != null);
        var noConsentOrder = orders.Single(o => o.WhatsAppConsentAtUtc == null);
        Assert.Equal("+96550000000", consentOrder.WhatsAppConsentPhone);
        Assert.Equal("Checkout", consentOrder.WhatsAppConsentSource);
        Assert.Null(noConsentOrder.WhatsAppConsentPhone);
        var audit = await db.WhatsAppConsentAudits.SingleAsync(a => a.OrderId == consentOrder.Id, Ct);
        Assert.Equal(WhatsAppConsentAction.Granted, audit.Action);
        Assert.Equal("+96550000000", audit.Phone);
    }

    [Fact]
    public async Task Account_phone_is_verified_before_opt_in_and_can_be_revoked()
    {
        using (var request = Auth(HttpMethod.Post, "/api/account/whatsapp/phone-verification",
                   new { phoneNumber = "+965 5000-0000" }))
            Assert.Equal(HttpStatusCode.Accepted, (await _client.SendAsync(request, Ct)).StatusCode);

        var sender = _factory.Services.GetRequiredService<TestGupshupSender>();
        Assert.Equal("+96550000000", sender.LastDestination);
        Assert.Matches("^[0-9]{6}$", sender.LastVerificationCode!);

        using (var request = Auth(HttpMethod.Post, "/api/account/whatsapp/phone-verification/confirm",
                   new { phoneNumber = "0096550000000", code = sender.LastVerificationCode }))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var request = Auth(HttpMethod.Put, "/api/account/whatsapp/preferences",
                   new { enabled = true, phoneNumber = "50000000" }))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var request = Auth(HttpMethod.Put, "/api/account/whatsapp/preferences",
                   new { enabled = false, phoneNumber = (string?)null }))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);

        await using var db = Db();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == UserId, Ct);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.False(user.WhatsAppNotificationsEnabled);
        Assert.Null(user.WhatsAppConsentPhone);
        Assert.NotNull(user.WhatsAppConsentRevokedAtUtc);
        var actions = await db.WhatsAppConsentAudits.Where(a => a.AppUserId == UserId)
            .OrderBy(a => a.Id).Select(a => a.Action).ToListAsync(Ct);
        Assert.Equal([WhatsAppConsentAction.Granted, WhatsAppConsentAction.Revoked], actions);
    }

    [Fact]
    public async Task Previously_skipped_invoice_requires_owner_consent_and_cannot_be_queued_twice()
    {
        int orderId;
        await using (var db = Db())
        {
            var order = new Order
            {
                AppUserId = UserId, OrderNumber = "OLD-CONSENT-1", Status = OrderStatus.Processing,
                Subtotal = 4m, Total = 4m,
                ShippingAddress = new ShippingAddress { FullName = "Customer", Phone = "+965 5000 0000", Street = "Street" }
            };
            var transaction = new Transaction { Order = order, AppUserId = UserId, Amount = 4m, PaymentMethod = PaymentMethod.Bank, Status = TransactionStatus.Paid };
            var attempt = new PaymentAttempt
            {
                Order = order, Transaction = transaction, Amount = 4m, Currency = "KWD", Status = PaymentAttemptStatus.Paid,
                IdempotencyKeyHash = new string('1', 64), RequestHash = new string('2', 64), MerchantReference = "OLD-CONSENT-PAY"
            };
            db.WhatsAppInvoices.Add(new WhatsAppInvoice
            {
                PaymentAttempt = attempt, InvoiceNumber = "INV-OLD-CONSENT-1",
                Status = WhatsAppInvoiceStatus.Skipped, LastErrorCode = "NO_MATCHING_CUSTOMER_CONSENT"
            });
            await db.SaveChangesAsync(Ct);
            orderId = order.Id;
        }

        using (var adminRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/whatsapp-invoice/retry")
               {
                   Content = JsonContent.Create(new { consent = true, whatsAppPhone = "50000000" })
               })
        {
            adminRequest.Headers.Add("X-Test-User", "shipping-admin");
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(adminRequest, Ct)).StatusCode);
        }
        using (var request = Auth(HttpMethod.Post, $"/api/orders/{orderId}/whatsapp-invoice/retry",
                   new { consent = true, whatsAppPhone = "00965 5000 0000" }))
            Assert.Equal(HttpStatusCode.Accepted, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var request = Auth(HttpMethod.Post, $"/api/orders/{orderId}/whatsapp-invoice/retry",
                   new { consent = true, whatsAppPhone = "50000000" }))
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(request, Ct)).StatusCode);

        await using var check = Db();
        var invoice = await check.WhatsAppInvoices.SingleAsync(i => i.PaymentAttempt.OrderId == orderId, Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Pending, invoice.Status);
        Assert.Null(invoice.ProviderMessageId);
        Assert.Equal(1, await check.WhatsAppConsentAudits.CountAsync(a => a.OrderId == orderId, Ct));
    }

    [Theory]
    [InlineData("+4915112345678", "004915112345678")]
    [InlineData("004915112345678", "+4915112345678")]
    public async Task German_checkout_persists_E164_and_rejects_mismatched_consent(string shipping, string consent)
    {
        using var accepted = await CheckoutAsync("german-consent", true, consent, shipping);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var rejected = await CheckoutAsync("wrong-recipient", true, "+96550000000", shipping);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var db = Db();
        var order = await db.Orders.AsNoTracking().Include(o => o.ShippingAddress).SingleAsync(Ct);
        Assert.Equal("+4915112345678", order.WhatsAppConsentPhone);
        Assert.Equal("+4915112345678", order.ShippingAddress.Phone);
        Assert.Equal("Checkout", order.WhatsAppConsentSource);
        Assert.NotNull(order.WhatsAppConsentAtUtc);
    }

    [Fact]
    public async Task German_account_requires_verification_and_changing_number_revokes_previous_consent()
    {
        using (var request = Auth(HttpMethod.Put, "/api/account/whatsapp/preferences",
            new { enabled = true, phoneNumber = "+4915112345678" }))
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var request = Auth(HttpMethod.Post, "/api/account/whatsapp/phone-verification",
            new { phoneNumber = "004915112345678" }))
            Assert.Equal(HttpStatusCode.Accepted, (await _client.SendAsync(request, Ct)).StatusCode);
        var sender = _factory.Services.GetRequiredService<TestGupshupSender>();
        Assert.Equal("+4915112345678", sender.LastDestination);
        using (var request = Auth(HttpMethod.Post, "/api/account/whatsapp/phone-verification/confirm",
            new { phoneNumber = "+4915112345678", code = sender.LastVerificationCode }))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var request = Auth(HttpMethod.Put, "/api/account/whatsapp/preferences",
            new { enabled = true, phoneNumber = "004915112345678" }))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        // Advance only this test's challenge timestamps past the server rate-limit window.
        await using (var db = Db())
            await db.WhatsAppPhoneVerifications.ExecuteUpdateAsync(set => set.SetProperty(v => v.CreatedAt, DateTime.UtcNow.AddMinutes(-2)), Ct);
        using (var request = Auth(HttpMethod.Post, "/api/account/whatsapp/phone-verification",
            new { phoneNumber = "+4915112345679" }))
            Assert.Equal(HttpStatusCode.Accepted, (await _client.SendAsync(request, Ct)).StatusCode);
        using (var get = new HttpRequestMessage(HttpMethod.Get, "/api/account/whatsapp/preferences"))
        {
            get.Headers.Add("X-Test-User", UserId);
            var response = await _client.SendAsync(get, Ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.False(json.GetProperty("enabled").GetBoolean());
        }
        using (var request = Auth(HttpMethod.Put, "/api/account/whatsapp/preferences",
            new { enabled = true, phoneNumber = "+4915112345679" }))
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(request, Ct)).StatusCode);
        await using var check = Db();
        Assert.True(await check.WhatsAppConsentAudits.AnyAsync(a => a.Phone == "+4915112345678" && a.Action == WhatsAppConsentAction.Revoked, Ct));
    }

    private async Task<HttpResponseMessage> CheckoutAsync(string key, bool optIn, string? consentPhone, string shippingPhone)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { productId = _productId, quantity = 1 } },
                shippingAddress = new { fullName = "Customer", phone = shippingPhone, street = "Street", city = "الخيران", state = "محافظة الأحمدي" },
                paymentMethod = (int)PaymentMethod.Cod,
                whatsAppInvoiceOptIn = optIn,
                whatsAppConsentPhone = consentPhone
            })
        };
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request, Ct);
    }

    private static HttpRequestMessage Auth(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Test-User", UserId);
        return request;
    }
}
