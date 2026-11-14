using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Ecom.Application.DTOs.Auth;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Ecom.CheckoutAudit.Tests;

public sealed class P1AccountSecurityTests : IAsyncLifetime
{
    private const string Original = "Synthetic!Password1";
    private const string Replacement = "Synthetic!Password2";
    private const string Owner = "owner@example.test";
    private const string Other = "other@example.test";
    private const string Admin = "admin@example.test";
    private const string Unconfirmed = "unconfirmed@example.test";
    private readonly string _database = $"ecom_p1_{Guid.NewGuid():N}";
    private readonly AccountTestClock _clock = new();
    private readonly ConcurrentDictionary<(string Email, string Method), string> _mail = new();
    private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private WebApplicationFactory<Program> _host = null!;
    private HttpClient _client = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private string Connection => PostgresTestDatabase.ConnectionString(_database);

    public async ValueTask InitializeAsync()
    {
        _host = CreateHost();
        _client = _host.CreateClient();
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        await db.Database.MigrateAsync(Ct);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUsers>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        foreach (var email in new[] { Owner, Other, Admin, Unconfirmed })
        {
            var user = new AppUsers { UserName = email, Email = email, EmailConfirmed = email != Unconfirmed };
            Assert.True((await users.CreateAsync(user, Original)).Succeeded);
            if (email == Admin)
                Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(Connection).Options);
        await db.Database.EnsureDeletedAsync(Ct);
    }

    [Fact]
    public async Task Removing_a_saved_address_keeps_existing_orders_and_financial_records_visible()
    {
        var owner = await LoginAsync(Owner);
        var admin = await LoginAsync(Admin);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var userId = await db.Users.Where(u => u.Email == Owner).Select(u => u.Id).SingleAsync(Ct);
        var address = new ShippingAddress { AppUserId = userId, FullName = "Historical recipient" };
        var order = new Order { AppUserId = userId, OrderNumber = "ADDRESS-HISTORY-FIXTURE", ShippingAddress = address,
            Total = 12.5m, Subtotal = 12.5m };
        var transaction = new Transaction { AppUserId = userId, Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Cod };
        db.Add(transaction);
        await db.SaveChangesAsync(Ct);

        using var removed = await SendAsync(HttpMethod.Delete, $"/api/auth/addresses/{address.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var hidden = await SendAsync(HttpMethod.Get, $"/api/auth/addresses/{address.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenFromAdminAddressBook = await SendAsync(HttpMethod.Get,
            $"/api/admin/auth/user/{userId}/addresses/{address.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.NotFound, hiddenFromAdminAddressBook.StatusCode);

        using var customerOrder = await SendAsync(HttpMethod.Get, $"/api/orders/{order.Id}", owner.Token);
        Assert.Equal(HttpStatusCode.OK, customerOrder.StatusCode);
        using var orderHistory = await SendAsync(HttpMethod.Get, "/api/orders/my-orders", owner.Token);
        Assert.Equal(HttpStatusCode.OK, orderHistory.StatusCode);
        Assert.Contains((await orderHistory.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct)).EnumerateArray(),
            entry => entry.GetProperty("id").GetInt32() == order.Id);
        using var retainedTransaction = await SendAsync(HttpMethod.Get, $"/api/admin/transaction/{transaction.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.OK, retainedTransaction.StatusCode);
        db.ChangeTracker.Clear();
        Assert.True(await db.ShippingAddresses.Where(a => a.Id == address.Id).Select(a => a.IsDeleted).SingleAsync(Ct));
        Assert.Equal(address.Id, await db.Orders.Where(o => o.Id == order.Id).Select(o => o.ShippingAddressId).SingleAsync(Ct));
        Assert.Equal(order.Total, await db.Transactions.Where(t => t.Id == transaction.Id).Select(t => t.Amount).SingleAsync(Ct));
    }

    [Fact]
    public async Task Customers_can_create_multiple_addresses_and_only_the_owner_can_change_them()
    {
        var owner = await LoginAsync(Owner);
        var other = await LoginAsync(Other);
        const string route = "/api/auth/addresses";
        var input = new { fullName = "Fixture recipient", addressLine1 = "Fixture address", phoneNumber = "+96550012345" };

        using var first = await SendAsync(HttpMethod.Post, route, owner.Token, input);
        using var second = await SendAsync(HttpMethod.Post, route, owner.Token, input with { addressLine1 = "Second address" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct)).GetProperty("data").GetProperty("id").GetInt32();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct)).GetProperty("data").GetProperty("id").GetInt32();
        Assert.NotEqual(firstId, secondId);
        var update = new { id = secondId, input.fullName, addressLine1 = "Owner update", input.phoneNumber };

        using var deniedRead = await SendAsync(HttpMethod.Get, $"{route}/{secondId}", other.Token);
        using var deniedUpdate = await SendAsync(HttpMethod.Put, route, other.Token, update);
        using var deniedDelete = await SendAsync(HttpMethod.Delete, $"{route}/{secondId}", other.Token);
        Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedDelete.StatusCode);

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        Assert.Equal("Second address", await db.ShippingAddresses.Where(a => a.Id == secondId)
            .Select(a => a.AddressLine1).SingleAsync(Ct));
        using var ownerUpdate = await SendAsync(HttpMethod.Put, route, owner.Token, update);
        Assert.Equal(HttpStatusCode.OK, ownerUpdate.StatusCode);
        Assert.Equal("Owner update", await db.ShippingAddresses.Where(a => a.Id == secondId)
            .Select(a => a.AddressLine1).SingleAsync(Ct));
        using var ownerDelete = await SendAsync(HttpMethod.Delete, $"{route}/{secondId}", owner.Token);
        Assert.Equal(HttpStatusCode.OK, ownerDelete.StatusCode);
        Assert.False(await db.ShippingAddresses.IgnoreQueryFilters().AnyAsync(a => a.Id == secondId, Ct));
        Assert.True(await db.ShippingAddresses.AnyAsync(a => a.Id == firstId, Ct));
    }

    [Fact]
    public async Task Catalog_search_keeps_summary_values_and_admin_search_keeps_product_details()
    {
        var admin = await LoginAsync(Admin);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var product = new Product
        {
            Title = "Fixture Search Product", newPrice = 12.5m, TotalInStock = 3,
            Images = ["/fixture.png"],
            subCategory = new SubCategory { Name = "Fixture Subcategory", Category = new Category { Name = "Fixture Category" } },
            Ratings = [new Rating { RatingNumber = 4, Content = "Synthetic review" }]
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(Ct);

        using var summary = await _client.GetAsync("/api/products?searchTerm=Fixture%20Search", Ct);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        var item = Assert.Single((await summary.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct)).EnumerateArray());
        Assert.Equal(product.Id, item.GetProperty("id").GetInt32());
        Assert.Equal("Fixture Subcategory", item.GetProperty("subCategoryName").GetString());
        Assert.Equal(4, item.GetProperty("averageRating").GetDouble());
        Assert.Equal(1, item.GetProperty("totalReviews").GetInt32());
        Assert.Equal("/fixture.png", item.GetProperty("mainImage").GetString());

        using var details = await SendAsync(HttpMethod.Get, "/api/admin/products?searchTerm=Fixture%20Search", admin.Token);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detail = Assert.Single((await details.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct))
            .GetProperty("items").EnumerateArray());
        Assert.Equal(product.Id, detail.GetProperty("id").GetInt32());
        Assert.Single(detail.GetProperty("ratings").EnumerateArray());
        Assert.Single(detail.GetProperty("images").EnumerateArray());
    }

    [Fact]
    public async Task Global_rate_limit_separates_real_accounts_from_each_other_and_from_anonymous_requests()
    {
        var owner = await LoginAsync(Owner);
        var other = await LoginAsync(Other);

        for (var i = 0; i < 100; i++)
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/auth/me", owner.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await SendAsync(HttpMethod.Get, "/api/auth/me", owner.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var unaffected = await SendAsync(HttpMethod.Get, "/api/auth/me", other.Token);
        Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
        using var anonymous = await _client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Transaction_deletion_preserves_ledger_values_and_payment_references()
    {
        var customer = await LoginAsync(Owner);
        var admin = await LoginAsync(Admin);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var order = new Order { OrderNumber = "FINANCIAL-RETENTION", Total = 12.5m, Subtotal = 12.5m,
            Status = OrderStatus.Delivered, ShippingAddress = new ShippingAddress { FullName = "Fixture" } };
        var transactions = new[]
        {
            new Transaction { Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Card,
                Status = TransactionStatus.Paid, TransactionReference = "verified", GatewayVerifiedAtUtc = DateTime.UtcNow },
            new Transaction { Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Card,
                Status = TransactionStatus.Refunded, TransactionReference = "refunded", IsRefunded = true, RefundAmount = 2.5m },
            new Transaction { Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Cod,
                Status = TransactionStatus.Collected, TransactionReference = "cash" },
            new Transaction { Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Bank,
                Status = TransactionStatus.Failed, TransactionReference = "historical", Notes = "Original ledger note" },
            new Transaction { Order = order, Amount = 12.5m, PaymentMethod = PaymentMethod.Card,
                Status = TransactionStatus.Paid, TransactionReference = "linked" }
        };
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync(Ct);
        var attempt = new PaymentAttempt { OrderId = order.Id, TransactionId = transactions[^1].Id,
            MerchantReference = "retention-attempt", IdempotencyKeyHash = "retention-key",
            RequestHash = "retention-request", Amount = 12.5m, Status = PaymentAttemptStatus.Paid };
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        var ledger = db.Transactions.IgnoreQueryFilters().Where(t => t.OrderId == order.Id)
            .OrderBy(t => t.Id).Select(t => new { t.Id, t.Status, t.Amount, t.PaymentMethod, t.TransactionReference,
                t.IsRefunded, t.RefundAmount, t.Notes, t.GatewayVerifiedAtUtc, t.IsDeleted, t.UpdatedAt });
        var before = await ledger.ToListAsync(Ct);
        var route = $"/api/admin/transaction/{transactions[0].Id}";
        using var anonymous = await _client.DeleteAsync(route, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var forbidden = await SendAsync(HttpMethod.Delete, route, customer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        foreach (var transaction in transactions)
        {
            var transactionRoute = $"/api/admin/transaction/{transaction.Id}";
            using var response = await SendAsync(HttpMethod.Delete, transactionRoute, admin.Token);
            Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
            using var visible = await SendAsync(HttpMethod.Get, transactionRoute, admin.Token);
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        }
        Assert.Equal(before, await ledger.ToListAsync(Ct));
        Assert.Equal(transactions[^1].Id, await db.PaymentAttempts.Where(p => p.Id == attempt.Id)
            .Select(p => p.TransactionId).SingleAsync(Ct));
        Assert.Equal(order.Total, await db.Orders.Where(o => o.Id == order.Id).Select(o => o.Total).SingleAsync(Ct));
    }

    [Fact]
    public async Task Production_bearer_identity_controls_customer_and_Admin_review_writes()
    {
        var owner = await LoginAsync(Owner);
        var other = await LoginAsync(Other);
        var admin = await LoginAsync(Admin);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var ownerId = await db.Users.Where(u => u.Email == Owner).Select(u => u.Id).SingleAsync(Ct);
        var adminId = await db.Users.Where(u => u.Email == Admin).Select(u => u.Id).SingleAsync(Ct);
        var product = new Product { Title = "Test product",
            subCategory = new SubCategory { Name = "Test subcategory", Category = new Category { Name = "Test category" } } };
        var order = new Order { OrderNumber = "P2-REVIEWS", AppUserId = ownerId, Status = OrderStatus.Delivered,
            ShippingAddress = new ShippingAddress { FullName = "Recipient" },
            Items = [new OrderItem { Product = product, Quantity = 1 }] };
        db.Add(order);
        await db.SaveChangesAsync(Ct);

        var input = new { productId = product.Id, orderId = order.Id, ratingNumber = 4, content = "Test review",
            reviewerUserId = adminId, createdBy = adminId };
        var path = $"/api/products/{product.Id}/ratings";
        var adminPath = $"/api/admin/products/{product.Id}/ratings";
        using var invalid = await SendAsync(HttpMethod.Post, path, owner.Token + "tampered", input);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var nonOwner = await SendAsync(HttpMethod.Post, path, other.Token, input);
        Assert.Equal(HttpStatusCode.Forbidden, nonOwner.StatusCode);
        using var notAdmin = await SendAsync(HttpMethod.Post, adminPath, other.Token, input);
        Assert.Equal(HttpStatusCode.Forbidden, notAdmin.StatusCode);
        using var review = await SendAsync(HttpMethod.Post, path, owner.Token, input);
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        using var adminReview = await SendAsync(HttpMethod.Post, adminPath, admin.Token, input with { createdBy = ownerId });
        Assert.Equal(HttpStatusCode.Created, adminReview.StatusCode);
        using var duplicate = await SendAsync(HttpMethod.Post, adminPath, admin.Token, input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var ratings = await db.Ratings.OrderBy(r => r.Id).ToListAsync(Ct);
        Assert.Equal(new[] { ownerId, adminId }, ratings.Select(r => r.ReviewerUserId));
        Assert.Equal(new[] { ownerId, adminId }, ratings.Select(r => r.CreatedBy));
    }

    [Fact]
    public async Task Arabic_usernames_work_at_registration_and_profile_update()
    {
        const string email = "arabic-registration@example.test";
        const string username = "أحمد ٢٢٣";
        using var registration = await RegisterAsync(username, email);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        await using var scope = _host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUsers>>();
        Assert.Equal(username, (await users.FindByEmailAsync(email))?.UserName);

        using var duplicate = await RegisterAsync(username, "duplicate@example.test");
        await AssertFieldErrorAsync(duplicate, "username", "DuplicateUserName");

        var owner = await LoginAsync(Owner);
        const string changed = "مُحمَّد 123";
        using var update = await SendAsync(HttpMethod.Put, "/api/auth/me/username", owner.Token, new { username = changed });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(changed, (await users.FindByEmailAsync(Owner))?.UserName);

        using var invalidUpdate = await SendAsync(HttpMethod.Put, "/api/auth/me/username", owner.Token,
            new { username = "مُحمَّد\u202e123" });
        await AssertFieldErrorAsync(invalidUpdate, "username", "InvalidUserName");
    }

    [Fact]
    public async Task Registration_errors_identify_username_and_password_separately()
    {
        using var invalidName = await RegisterAsync("أحمد/123", "invalid-name@example.test");
        await AssertFieldErrorAsync(invalidName, "username", "InvalidUserName");

        using var invalidPassword = await RegisterAsync("أحمد123", "invalid-password@example.test", "Synthetic!Password");
        await AssertFieldErrorAsync(invalidPassword, "password", "PasswordRequiresDigit");
    }

    private Task<HttpResponseMessage> RegisterAsync(string username, string email, string password = Original) =>
        _client.PostAsJsonAsync("/api/auth/register", new { username, email, phoneNumber = "1234567890",
            password, confirmPassword = password }, Ct);

    private async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AccountValidationResponseDto>(Ct);
        Assert.NotNull(payload?.FieldErrors);
        var error = Assert.Single(payload.FieldErrors);
        Assert.Equal(field, error.Key);
        Assert.Contains(code, error.Value);
    }

    [Fact]
    public async Task Concurrent_refresh_across_hosts_redeems_once_and_stores_only_the_winning_hash()
    {
        var login = await LoginAsync(Owner);
        var secondSession = await LoginAsync(Owner);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        Assert.Equal(login.ExpiresAt, jwt.ValidTo);
        Assert.InRange(login.ExpiresAt - _clock.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(16), TimeSpan.FromMinutes(17));
        await using var otherHost = CreateHost();
        using var otherClient = otherHost.CreateClient();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            RefreshAsync(login.RefreshToken, i % 2 == 0 ? _client : otherClient)));
        try
        {
            var winner = Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.OK);
            Assert.All(attempts.Where(r => r != winner), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
            var rotated = await ReadSessionAsync(winner);
            Assert.Equal(rotated.ExpiresAt, new JwtSecurityTokenHandler().ReadJwtToken(rotated.Token).ValidTo);
            Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);
            await using var scope = _host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
            var hashes = await db.AuthSessions.Select(s => s.RefreshTokenHash).ToListAsync(Ct);
            Assert.Equal(2, hashes.Count);
            Assert.Contains(Hash(rotated.RefreshToken), hashes);
            Assert.Contains(Hash(secondSession.RefreshToken), hashes);
            Assert.DoesNotContain(Hash(login.RefreshToken), hashes);
            using var replay = await RefreshAsync(login.RefreshToken);
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
            using var next = await RefreshAsync(rotated.RefreshToken);
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        }
        finally { foreach (var attempt in attempts) attempt.Dispose(); }
    }

    [Fact]
    public async Task Logout_revokes_only_current_session_and_expired_sessions_cannot_be_used()
    {
        var first = await LoginAsync(Owner);
        var second = await LoginAsync(Owner);
        using var logout = await SendAsync(HttpMethod.Post, "/api/auth/logout", first.Token);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        await AssertRevokedAsync(first);
        using var stillValid = await SendAsync(HttpMethod.Get, "/api/auth/me", second.Token);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        _clock.Advance(TimeSpan.FromDays(8));
        // Framework JWT clock has not advanced; the database session must still reject it.
        await AssertRevokedAsync(second);
    }

    [Fact]
    public async Task Block_revokes_admin_and_user_sessions_even_after_unblock_and_admin_role_is_checked_live()
    {
        var owner = await LoginAsync(Owner);
        var blockedAdmin = await LoginAsync(Admin);
        var other = await LoginAsync(Other);
        await using var scope = _host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUsers>>();
        var otherUser = (await users.FindByEmailAsync(Other))!;
        Assert.True((await users.AddToRoleAsync(otherUser, "Admin")).Succeeded);
        var operatorSession = await LoginAsync(Other);

        using var before = await SendAsync(HttpMethod.Get, "/api/admin/auth/isAdmin", blockedAdmin.Token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var ownerBlock = await SendAsync(HttpMethod.Post, "/api/admin/user-manager/users/block", operatorSession.Token,
            new { userId = owner.User.Id, reason = "Synthetic block" });
        Assert.Equal(HttpStatusCode.OK, ownerBlock.StatusCode);
        using var adminBlock = await SendAsync(HttpMethod.Post, "/api/admin/user-manager/users/block", operatorSession.Token,
            new { userId = blockedAdmin.User.Id, reason = "Synthetic block" });
        Assert.Equal(HttpStatusCode.OK, adminBlock.StatusCode);
        await AssertRevokedAsync(owner);
        await AssertRevokedAsync(blockedAdmin);
        using var privileged = await SendAsync(HttpMethod.Get, "/api/admin/auth/isAdmin", blockedAdmin.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, privileged.StatusCode);
        using var blockedLogin = await _client.PostAsJsonAsync("/api/auth/login", new { email = Admin, password = Original }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);
        using var unblock = await SendAsync(HttpMethod.Post, "/api/admin/user-manager/users/unblock", operatorSession.Token,
            new { userId = blockedAdmin.User.Id });
        Assert.Equal(HttpStatusCode.OK, unblock.StatusCode);
        await AssertRevokedAsync(blockedAdmin);

        Assert.True((await users.RemoveFromRoleAsync(otherUser, "Admin")).Succeeded);
        using var demoted = await SendAsync(HttpMethod.Get, "/api/admin/auth/isAdmin", operatorSession.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, demoted.StatusCode);
        using var ordinary = await SendAsync(HttpMethod.Get, "/api/auth/me", other.Token);
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
    }

    [Fact]
    public async Task Legacy_signed_access_token_without_session_is_rejected()
    {
        var login = await LoginAsync(Owner);
        var token = new JwtSecurityToken("p1-tests", "p1-tests", [new(System.Security.Claims.ClaimTypes.NameIdentifier, login.User.Id)],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_signingKey)),
                SecurityAlgorithms.HmacSha256));
        using var response = await SendAsync(HttpMethod.Get, "/api/auth/me", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Password_change_and_both_admin_password_routes_preserve_failed_credentials_and_revoke_on_success()
    {
        var first = await LoginAsync(Owner);
        var second = await LoginAsync(Owner);
        var admin = await LoginAsync(Admin);
        using var invalid = await SendAsync(HttpMethod.Post, "/api/auth/change-password", first.Token,
            new { currentPassword = Original, newPassword = "weakpass", confirmNewPassword = "weakpass" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var changed = await SendAsync(HttpMethod.Post, "/api/auth/change-password", first.Token,
            new { currentPassword = Original, newPassword = Replacement, confirmNewPassword = Replacement });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await AssertRevokedAsync(first);
        await AssertRevokedAsync(second);

        var third = await LoginAsync(Owner, Replacement);
        using var adminInvalid = await SendAsync(HttpMethod.Post, $"/api/admin/user-manager/users/{third.User.Id}/reset-password", admin.Token, "weakpass");
        Assert.Equal(HttpStatusCode.BadRequest, adminInvalid.StatusCode);
        await LoginAsync(Owner, Replacement);
        using var adminChange = await SendAsync(HttpMethod.Post, "/api/admin/user-manager/users/change-password", admin.Token,
            new { userId = third.User.Id, newPassword = Original, confirmNewPassword = Original });
        Assert.Equal(HttpStatusCode.OK, adminChange.StatusCode);
        await AssertRevokedAsync(third);

        var fourth = await LoginAsync(Owner);
        using var adminReset = await SendAsync(HttpMethod.Post, $"/api/admin/user-manager/users/{fourth.User.Id}/reset-password", admin.Token, Replacement);
        Assert.Equal(HttpStatusCode.OK, adminReset.StatusCode);
        await AssertRevokedAsync(fourth);
        await LoginAsync(Owner, Replacement);
    }

    [Fact]
    public async Task Recovery_is_generic_rate_limited_and_only_mails_eligible_accounts()
    {
        using var known = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Owner }, Ct);
        using var unknown = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@example.test" }, Ct);
        Assert.Equal(await known.Content.ReadAsStringAsync(Ct), await unknown.Content.ReadAsStringAsync(Ct));
        Assert.Single(_mail);
        using var unconfirmedLogin = await _client.PostAsJsonAsync("/api/auth/login", new { email = Unconfirmed, password = "wrong" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unconfirmedLogin.StatusCode);
        Assert.Single(_mail);
        for (var i = 0; i < 8; i++)
        {
            using var permitted = await _client.PostAsJsonAsync("/api/auth/resend-verification", new { email = Owner }, Ct);
            Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);
        }
        using var limited = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Owner }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Single(_mail);
    }

    [Fact]
    public async Task Email_verification_is_single_use_and_does_not_authorize_password_reset()
    {
        using var issued = await _client.PostAsJsonAsync("/api/auth/resend-verification", new { email = Unconfirmed }, Ct);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var code = _mail[(Unconfirmed, nameof(IEmailService.SendOtpEmailAsync))];
        using var wrongPurpose = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Unconfirmed, otp = code, newPassword = Replacement, confirmNewPassword = Replacement }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            _client.PostAsJsonAsync("/api/auth/verify", new { email = Unconfirmed, otp = code }, Ct)));
        try
        {
            Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal(2, attempts.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        }
        finally { foreach (var attempt in attempts) attempt.Dispose(); }
        await LoginAsync(Unconfirmed);
    }

    [Fact]
    public async Task Challenges_are_bound_to_purpose_and_user_with_durable_issuance_and_attempt_limits()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUsers>>();
        var user = (await users.FindByEmailAsync(Owner))!;
        var other = (await users.FindByEmailAsync(Other))!;
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var verify = (await otp.IssueAsync(user, AccountChallengePurpose.EmailVerification))!;
        Assert.False(await otp.ConsumeAsync(user, AccountChallengePurpose.PasswordRecovery, verify));
        Assert.False(await otp.ConsumeAsync(other, AccountChallengePurpose.EmailVerification, verify));
        Assert.Null(await otp.IssueAsync(user, AccountChallengePurpose.EmailVerification));

        var recovery = (await otp.IssueAsync(user, AccountChallengePurpose.PasswordRecovery))!;
        var bad = recovery == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.False(await otp.ConsumeAsync(user, AccountChallengePurpose.PasswordRecovery, bad));
        Assert.False(await otp.ConsumeAsync(user, AccountChallengePurpose.PasswordRecovery, recovery));

        await using var otherScope = _host.Services.CreateAsyncScope();
        var competing = otherScope.ServiceProvider.GetRequiredService<IOtpService>();
        var results = await Task.WhenAll(otp.ConsumeAsync(user, AccountChallengePurpose.EmailVerification, verify),
            competing.ConsumeAsync(user, AccountChallengePurpose.EmailVerification, verify));
        Assert.Single(results, r => r);
        for (var i = 0; i < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            Assert.NotNull(await competing.IssueAsync(user, AccountChallengePurpose.EmailVerification));
        }
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(await otp.IssueAsync(user, AccountChallengePurpose.EmailVerification));
    }

    [Fact]
    public async Task Reset_rejects_expiry_and_replay_and_preserves_old_password_on_identity_validation_failure()
    {
        var first = await LoginAsync(Owner);
        var expired = await RequestResetAsync();
        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(HttpStatusCode.BadRequest, await ResetAsync(expired, Replacement));
        await LoginAsync(Owner);

        var failed = await RequestResetAsync();
        Assert.Equal(HttpStatusCode.BadRequest, await ResetAsync(failed, "weakpass"));
        Assert.Equal(HttpStatusCode.BadRequest, await ResetAsync(failed, Replacement));
        await LoginAsync(Owner);
        using var unchanged = await SendAsync(HttpMethod.Get, "/api/auth/me", first.Token);
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var valid = await RequestResetAsync();
        var outcomes = await Task.WhenAll(ResetAsync(valid, Replacement), ResetAsync(valid, Replacement));
        Assert.Single(outcomes, status => status == HttpStatusCode.OK);
        Assert.Single(outcomes, status => status == HttpStatusCode.BadRequest);
        await AssertRevokedAsync(first);
        Assert.Equal(HttpStatusCode.BadRequest, await ResetAsync(valid, Original));
        await LoginAsync(Owner, Replacement);
    }

    [Fact]
    public async Task Database_failure_after_identity_write_rolls_back_password_challenge_and_revocation()
    {
        var login = await LoginAsync(Owner);
        var code = await RequestResetAsync();
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_revoke() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'Synthetic revocation write failure'; END; $$;
            CREATE TRIGGER test_fail_revoke BEFORE UPDATE ON "AuthSessions"
                FOR EACH ROW WHEN (NEW."RevokedAtUtc" IS NOT NULL) EXECUTE FUNCTION test_fail_revoke();
            """, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, await ResetAsync(code, Replacement));
        await LoginAsync(Owner);
        using var stillValid = await SendAsync(HttpMethod.Get, "/api/auth/me", login.Token);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);

        await db.Database.ExecuteSqlRawAsync("""DROP TRIGGER test_fail_revoke ON "AuthSessions";""", Ct);
        Assert.Equal(HttpStatusCode.OK, await ResetAsync(code, Replacement));
        await AssertRevokedAsync(login);
        await LoginAsync(Owner, Replacement);
    }

    [Fact]
    public async Task Coupons_expose_only_public_promotions_or_owned_codes_and_ignore_posted_identity()
    {
        var owner = await LoginAsync(Owner);
        var other = await LoginAsync(Other);
        var admin = await LoginAsync(Admin);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EcomDbContext>();
        var listed = Coupon("PUBLIC", isPublic: true);
        var unlisted = Coupon("UNLISTED");
        var assigned = Coupon("ASSIGNED", owner.User.Id);
        db.AddRange(listed, unlisted, assigned);
        await db.SaveChangesAsync(Ct);
        foreach (var path in new[] { "/api/coupons", "/api/coupons/active" })
        {
            using var response = await _client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var entries = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var promotion = Assert.Single(entries.EnumerateArray());
            Assert.Equal(listed.Code, promotion.GetProperty("code").GetString());
            AssertStorefrontFields(promotion);
        }

        foreach (var path in new[] { $"/api/coupons/{assigned.Id}", $"/api/coupons/code/{assigned.Code}" })
        {
            using var anon = await _client.GetAsync(path, Ct);
            using var forbidden = await SendAsync(HttpMethod.Get, path, other.Token);
            using var owned = await SendAsync(HttpMethod.Get, path, owner.Token);
            Assert.Equal(HttpStatusCode.NotFound, anon.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
            Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
            AssertStorefrontFields(await owned.Content.ReadFromJsonAsync<JsonElement>(Ct));
        }
        using var myCoupons = await SendAsync(HttpMethod.Get, "/api/coupons/my-coupons", owner.Token);
        Assert.Equal(assigned.Code, Assert.Single((await myCoupons.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray())
            .GetProperty("code").GetString());
        using var unlistedRead = await _client.GetAsync($"/api/coupons/{unlisted.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unlistedRead.StatusCode);
        using var adminRead = await SendAsync(HttpMethod.Get, $"/api/admin/coupons/{assigned.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
        Assert.Equal(owner.User.Id, (await adminRead.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("appUserId").GetString());

        const string validation = "/api/coupons/validate";
        using var anonymousPreview = await SendAsync(HttpMethod.Post, validation, body: new { code = assigned.Code, orderAmount = 20, userId = owner.User.Id });
        using var spoofed = await SendAsync(HttpMethod.Post, validation, other.Token, new { code = assigned.Code, orderAmount = 20, userId = owner.User.Id });
        using var missing = await SendAsync(HttpMethod.Post, validation, other.Token, new { code = "MISSING", orderAmount = 20 });
        Assert.Equal(await missing.Content.ReadAsStringAsync(Ct), await spoofed.Content.ReadAsStringAsync(Ct));
        Assert.Equal(await missing.Content.ReadAsStringAsync(Ct), await anonymousPreview.Content.ReadAsStringAsync(Ct));
        using var ownPreview = await SendAsync(HttpMethod.Post, validation, owner.Token, new { code = assigned.Code, orderAmount = 20, userId = other.User.Id });
        Assert.True((await ownPreview.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("isValid").GetBoolean());
        using var unlistedPreview = await SendAsync(HttpMethod.Post, validation, body: new { code = unlisted.Code, orderAmount = 20 });
        var preview = await unlistedPreview.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(preview.GetProperty("isValid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("coupon").ValueKind);
        Assert.Equal(2m, preview.GetProperty("discountAmount").GetDecimal());

        var coupons = scope.ServiceProvider.GetRequiredService<ICouponService>();
        await Assert.ThrowsAsync<ArgumentException>(() => coupons.ApplyCouponToOrderAsync(assigned.Code, 20, other.User.Id));
        Assert.Equal(assigned.Id, (await coupons.ApplyCouponToOrderAsync(assigned.Code, 20, owner.User.Id)).Id);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repo = new CouponRepository(db, cache);
        Assert.False(await repo.TryReserveUsageAsync(assigned.Id, DateTime.UtcNow, other.User.Id));
        Assert.True(await repo.TryReserveUsageAsync(assigned.Id, DateTime.UtcNow, owner.User.Id));
    }

    private static void AssertStorefrontFields(JsonElement coupon)
    {
        foreach (var field in new[] { "appUserId", "userName", "usedCount", "remainingUses", "usageLimit", "isFullyUsed" })
            Assert.False(coupon.TryGetProperty(field, out _), field);
    }

    private static Coupon Coupon(string code, string? userId = null, bool isPublic = false) => new()
    {
        Code = code, Description = "Synthetic coupon", Value = 10, Type = CouponType.Percentage,
        StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
        IsActive = true, IsPublic = isPublic, AppUserId = userId
    };

    private async Task AssertRevokedAsync(LoginResponseDto session)
    {
        using var bearer = await SendAsync(HttpMethod.Get, "/api/auth/me", session.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, bearer.StatusCode);
        using var refresh = await RefreshAsync(session.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    private async Task<LoginResponseDto> LoginAsync(string email, string password = Original)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);
        return await ReadSessionAsync(response);
    }

    private async Task<LoginResponseDto> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponseDto<LoginResponseDto>>(Ct))!.Data!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string token, HttpClient? client = null) =>
        (client ?? _client).PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token }, Ct);

    private async Task<string> RequestResetAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Owner }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return _mail[(Owner, nameof(IEmailService.SendPasswordResetEmailAsync))];
    }

    private async Task<HttpStatusCode> ResetAsync(string code, string password)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Owner, otp = code, newPassword = password, confirmNewPassword = password }, Ct);
        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request, Ct);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(token)));

    private WebApplicationFactory<Program> CreateHost() => new AccountApiFactory(Connection, _signingKey, _clock,
        InterfaceStub.Create<IEmailService>((method, args) =>
        {
            if (method.Name is nameof(IEmailService.SendPasswordResetEmailAsync) or nameof(IEmailService.SendOtpEmailAsync))
                _mail[(((AppUsers)args![0]!).Email!, method.Name)] = (string)args[1]!;
            return Task.FromResult(true);
        }));

    private sealed class AccountTestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class AccountApiFactory(string connection, string key, TimeProvider clock, IEmailService email)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = key, ["JwtSettings:Issuer"] = "p1-tests", ["JwtSettings:Audience"] = "p1-tests",
                    ["JwtSettings:ExpiryMinutes"] = "17", ["Sadad:BaseUrl"] = "https://sadad.test/",
                    ["AIShopping:Enabled"] = "false", ["Telegram:Enabled"] = "false", ["DatabaseBackup:Enabled"] = "false",
                    ["Monitoring:Directory"] = Path.Combine(Path.GetTempPath(), "ecom-p1-monitoring", Guid.NewGuid().ToString("N"))
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<EcomDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<EcomDbContext>>();
                services.AddDbContext<EcomDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.EnableRetryOnFailure()));
                services.RemoveAll<IEmailService>();
                services.AddSingleton(email);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            });
        }
    }
}
