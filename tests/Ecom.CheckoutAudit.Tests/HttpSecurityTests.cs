using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ecom.CheckoutAudit.Tests;

public sealed class HttpSecurityTests : IAsyncLifetime
{
    private const string GuestSecret = "http-guest-order-secret-with-enough-entropy";
    private readonly string _databaseName = $"EcomCheckoutHttp_{Guid.NewGuid():N}";
    private CheckoutApiFactory _factory = null!;
    private HttpClient _client = null!;
    private int _guestOrderId;
    private int _userOrderId;
    private int _productId;

    public async ValueTask InitializeAsync()
    {
        _factory = new CheckoutApiFactory(ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _factory.DisposeAsync();
    }

    private string ConnectionString =>
        PostgresTestDatabase.ConnectionString(_databaseName);

    [Fact]
    public async Task WhatsApp_management_requires_admin_and_public_pdf_requires_capability()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/whatsapp/admin/invoices", TestContext.Current.CancellationToken)).StatusCode);
        using var user = new HttpRequestMessage(HttpMethod.Get, "/api/whatsapp/admin/invoices");
        user.Headers.Add("X-Test-User", "user-a");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(user, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/whatsapp/invoices/1/document", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/whatsapp/webhook?token=incorrect", new { app = "whatsappaljawhara" },
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Legacy_fake_paid_attempt_is_rejected_at_http_boundary()
    {
        var response = await _client.PostAsJsonAsync($"/api/orders/{_guestOrderId}/payment", new
        {
            orderId = _guestOrderId,
            amount = 250,
            paymentMethod = (int)PaymentMethod.Bank,
            status = (int)TransactionStatus.Paid
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Guest_order_details_require_the_exact_order_capability()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/orders/number/HTTP-GUEST", TestContext.Current.CancellationToken)).StatusCode);

        using var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/orders/number/HTTP-GUEST");
        wrong.Headers.Add("X-Guest-Order-Token", "wrong-secret");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(wrong, TestContext.Current.CancellationToken)).StatusCode);

        using var valid = new HttpRequestMessage(HttpMethod.Get, "/api/orders/number/HTTP-GUEST");
        valid.Headers.Add("X-Guest-Order-Token", GuestSecret);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.SendAsync(valid, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_cannot_read_another_users_or_a_guest_order()
    {
        using var otherUsersOrder = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{_userOrderId}");
        otherUsersOrder.Headers.Add("X-Test-User", "user-a");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(otherUsersOrder, TestContext.Current.CancellationToken)).StatusCode);

        using var guestOrder = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{_guestOrderId}");
        guestOrder.Headers.Add("X-Test-User", "user-a");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(guestOrder, TestContext.Current.CancellationToken)).StatusCode);

        using var owner = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{_userOrderId}");
        owner.Headers.Add("X-Test-User", "user-b");
        Assert.Equal(HttpStatusCode.OK,
            (await _client.SendAsync(owner, TestContext.Current.CancellationToken)).StatusCode);

        using var cancel = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{_guestOrderId}/cancel");
        cancel.Headers.Add("X-Test-User", "user-a");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(cancel, TestContext.Current.CancellationToken)).StatusCode);

        using var paymentLink = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{_guestOrderId}/payment-link");
        paymentLink.Headers.Add("X-Test-User", "user-a");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(paymentLink, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_guest_checkout_and_cod_remain_functional_with_hashed_access_secret()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            items = new[]
            {
                new { productId = _productId, quantity = 2 },
                new { productId = _productId, quantity = 2 }
            },
            shippingAddress = new { fullName = "Checkout Guest", phone = "22222222", street = "Checkout Street" },
            paymentMethod = (int)PaymentMethod.Cod
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var rawToken = payload.GetProperty("guestAccessToken").GetString();
        var orderId = payload.GetProperty("orderId").GetInt32();
        Assert.NotNull(rawToken);
        Assert.True(rawToken!.Length >= 43);
        Assert.Equal((int)TransactionStatus.Pending, payload.GetProperty("status").GetInt32());
        Assert.False(payload.TryGetProperty("paymentUrl", out var url) && url.ValueKind == JsonValueKind.String);
        Assert.Equal(0, _factory.SadadHandler.Calls);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var order = await context.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId, TestContext.Current.CancellationToken);
        Assert.NotEqual(rawToken, order.GuestAccessTokenHash);
        Assert.Equal(Hash(rawToken), order.GuestAccessTokenHash);
        Assert.Single(order.Items);
        Assert.Equal(4, order.Items[0].Quantity);
        Assert.Equal(1, await context.Products.Where(item => item.Id == _productId).Select(item => item.TotalInStock).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Fifty_concurrent_requests_with_same_idempotency_key_create_one_checkout()
    {
        await using var secondFactory = new CheckoutApiFactory(ConnectionString);
        using var secondClient = secondFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string key = "phase2-same-checkout-key-with-high-entropy-001";
        var payload = new
        {
            items = new[] { new { productId = _productId, quantity = 1 } },
            shippingAddress = new { fullName = "Idempotent Guest", phone = "33333333", street = "Idempotent Street" },
            paymentMethod = (int)PaymentMethod.Cod
        };
        async Task<HttpResponseMessage> SendAsync(HttpClient client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        var responses = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => SendAsync(i % 2 == 0 ? _client : secondClient)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Single(bodies.Select(b => b.GetProperty("orderId").GetInt32()).Distinct());
        Assert.Single(bodies.Select(b => b.GetProperty("guestAccessToken").GetString()).Distinct());

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        Assert.Equal(1, await context.Orders.CountAsync(o => o.CheckoutIdempotencyKeyHash != null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Electronic_checkout_returns_explicit_pending_initialization_and_persists_outbox_without_inline_http()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { productId = _productId, quantity = 1 } },
                shippingAddress = new { fullName = "Electronic Guest", phone = "55555555", street = "Electronic Street" },
                paymentMethod = (int)PaymentMethod.Bank
            })
        };
        request.Headers.Add("Idempotency-Key", "electronic-init-key-with-high-entropy-003");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, body.GetProperty("paymentInitializationStatus").GetInt32());
        Assert.True(body.GetProperty("paymentAttemptId").GetInt32() > 0);
        Assert.Equal(0, _factory.SadadHandler.Calls);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        Assert.Equal(PaymentAttemptStatus.PendingDispatch, await context.PaymentAttempts.Select(a => a.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Payment_retry_has_its_own_idempotency_key_and_does_not_duplicate_an_active_attempt()
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId = _productId, quantity = 1 } }, shippingAddress = new { fullName = "Retry Guest", phone = "88888888", street = "Street" }, paymentMethod = (int)PaymentMethod.Bank })
        };
        create.Headers.Add("Idempotency-Key", "retry-checkout-key-with-high-entropy-005");
        var created = await _client.SendAsync(create, TestContext.Current.CancellationToken);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var orderId = body.GetProperty("orderId").GetInt32();
        var guestToken = body.GetProperty("guestAccessToken").GetString();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
            await context.PaymentAttempts.Where(a => a.OrderId == orderId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.Failed), TestContext.Current.CancellationToken);
        }
        async Task<HttpResponseMessage> RetryAsync()
        {
            using var retry = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payment-attempts");
            retry.Headers.Add("X-Guest-Order-Token", guestToken);
            retry.Headers.Add("Idempotency-Key", "retry-attempt-key-with-high-entropy-006");
            return await _client.SendAsync(retry, TestContext.Current.CancellationToken);
        }
        var first = await RetryAsync();
        var second = await RetryAsync();
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(firstBody.GetProperty("paymentAttemptId").GetInt32(), secondBody.GetProperty("paymentAttemptId").GetInt32());
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<EcomDbContext>();
        Assert.Equal(2, await verification.PaymentAttempts.CountAsync(a => a.OrderId == orderId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_distinct_checkouts_receive_unique_order_numbers()
    {
        async Task<HttpResponseMessage> SendAsync(int index)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(new { items = new[] { new { productId = _productId, quantity = 1 } }, shippingAddress = new { fullName = $"Guest {index}", phone = "66666666", street = "Street" }, paymentMethod = (int)PaymentMethod.Cod })
            };
            request.Headers.Add("Idempotency-Key", $"distinct-order-key-{index:D2}-with-high-entropy");
            return await _client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(SendAsync));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var numbers = await context.Orders.Where(o => o.CheckoutIdempotencyKeyHash != null).Select(o => o.OrderNumber).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, numbers.Distinct().Count());
    }

    [Fact]
    public async Task Cod_cancellation_releases_confirmed_inventory_exactly_once()
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { items = new[] { new { productId = _productId, quantity = 1 } }, shippingAddress = new { fullName = "COD Guest", phone = "77777777", street = "Street" }, paymentMethod = (int)PaymentMethod.Cod })
        };
        create.Headers.Add("Idempotency-Key", "cod-cancel-key-with-high-entropy-004");
        var created = await _client.SendAsync(create, TestContext.Current.CancellationToken);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        var orderId = body.GetProperty("orderId").GetInt32();
        var token = body.GetProperty("guestAccessToken").GetString();
        async Task<HttpResponseMessage> CancelAsync()
        {
            using var cancel = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{orderId}/cancel");
            cancel.Headers.Add("X-Guest-Order-Token", token);
            return await _client.SendAsync(cancel, TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await CancelAsync()).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        Assert.Equal(5, await context.Products.Where(p => p.Id == _productId).Select(p => p.TotalInStock).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ReservationStatus.Released, await context.InventoryReservations.Where(r => r.OrderId == orderId).Select(r => r.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Same_idempotency_key_with_different_payload_returns_conflict()
    {
        const string key = "phase2-conflict-key-with-high-entropy-002";
        async Task<HttpResponseMessage> SendAsync(int quantity)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(new
                {
                    items = new[] { new { productId = _productId, quantity } },
                    shippingAddress = new { fullName = "Conflict Guest", phone = "44444444", street = "Conflict Street" },
                    paymentMethod = (int)PaymentMethod.Cod
                })
            };
            request.Headers.Add("Idempotency-Key", key);
            return await _client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(2)).StatusCode);
    }

    [Fact]
    public async Task Undocumented_sadad_confirmation_stays_pending_and_fails_closed()
    {
        var response = await _client.GetAsync(
            "/api/orders/payment-callback?invoice_id=7001&payment=success",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.SadadHandler.Calls);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var transaction = await context.Transactions.SingleAsync(item => item.GatewayInvoiceId == "7001", TestContext.Current.CancellationToken);
        var order = await context.Orders.SingleAsync(item => item.Id == transaction.OrderId, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Guest_cancellation_requires_capability_and_revokes_it()
    {
        using var cancel = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{_guestOrderId}/cancel");
        cancel.Headers.Add("X-Guest-Order-Token", GuestSecret);
        var response = await _client.SendAsync(cancel, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var reuse = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{_guestOrderId}");
        reuse.Headers.Add("X-Guest-Order-Token", GuestSecret);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.SendAsync(reuse, TestContext.Current.CancellationToken)).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var order = await context.Orders.SingleAsync(item => item.Id == _guestOrderId, TestContext.Current.CancellationToken);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.GuestAccessRevokedAtUtc);
    }

    private async Task SeedAsync(EcomDbContext context)
    {
        var owner = new AppUsers
        {
            Id = "user-b",
            UserName = "user-b@example.test",
            NormalizedUserName = "USER-B@EXAMPLE.TEST",
            Email = "user-b@example.test",
            NormalizedEmail = "USER-B@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        var guestAddress = new ShippingAddress { FullName = "Guest", Phone = "00000000", Street = "Guest Street" };
        var userAddress = new ShippingAddress { FullName = "Owner", Phone = "11111111", Street = "Owner Street", AppUser = owner };
        var guestOrder = new Order
        {
            OrderNumber = "HTTP-GUEST",
            ShippingAddress = guestAddress,
            Subtotal = 10,
            Total = 10,
            Status = OrderStatus.Pending,
            GuestAccessTokenHash = Hash(GuestSecret),
            GuestAccessExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        };
        var userOrder = new Order
        {
            OrderNumber = "HTTP-USER-B",
            ShippingAddress = userAddress,
            AppUser = owner,
            Subtotal = 20,
            Total = 20,
            Status = OrderStatus.Pending
        };
        var pending = new Transaction
        {
            Order = guestOrder,
            Amount = 10,
            PaymentMethod = PaymentMethod.Bank,
            Status = TransactionStatus.Pending,
            GatewayInvoiceId = "7001"
        };
        var category = new Category { Name = "HTTP Category", NameAr = "اختبار" };
        var subCategory = new SubCategory { Name = "HTTP Subcategory", NameAr = "اختبار", Category = category };
        var product = new Product
        {
            Title = "HTTP Product",
            TitleAr = "اختبار",
            Description = "Synthetic",
            DescriptionAr = "اختبار",
            subCategory = subCategory,
            oldPrice = 3,
            newPrice = 3,
            TotalInStock = 5,
            IsInStock = true
        };
        context.AddRange(owner, guestAddress, userAddress, guestOrder, userOrder, pending, category, subCategory, product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _guestOrderId = guestOrder.Id;
        _userOrderId = userOrder.Id;
        _productId = product.Id;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed class CheckoutApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public CountingSadadHandler SadadHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["JwtSettings:SecretKey"] = "test-only-signing-key-that-is-at-least-thirty-two-bytes",
                ["JwtSettings:Issuer"] = "EcomTaaniAPI",
                ["JwtSettings:Audience"] = "EcomTaaniUsers",
                ["Sadad:BaseUrl"] = "https://sadad.test/"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EcomDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EcomDbContext>>();
            services.AddDbContext<EcomDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });

            services.AddHttpClient("Sadad")
                .ConfigurePrimaryHttpMessageHandler(() => SadadHandler);
        });
    }
}

internal sealed class CountingSadadHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(SadadHandler.Json("{\"isValid\":true,\"response\":{\"status\":\"Paid\",\"amount\":0.001,\"currency_Code\":\"USD\",\"ref_Number\":\"wrong\"}}"));
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CheckoutAuditTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId) || string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, userId.ToString())],
            SchemeName);
        if (userId == "shipping-admin") identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
