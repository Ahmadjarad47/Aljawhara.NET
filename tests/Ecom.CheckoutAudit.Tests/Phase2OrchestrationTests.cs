using System.Text.Json;
using AutoMapper;
using Ecom.Application.Payments;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

public sealed class Phase2OrchestrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"EcomPhase2_{Guid.NewGuid():N}";
    private string ConnectionString => PostgresTestDatabase.ConnectionString(_databaseName);

    public async ValueTask InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Two_independent_workers_claim_one_invoice_dispatch()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.PendingDispatch, OutboxStatus.Pending);
        var gateway = new StubGateway(new(GatewayDispatchOutcome.Created, "INV-1", "https://pay.test/INV-1"));
        await using var db1 = CreateContext();
        await using var db2 = CreateContext();
        var worker1 = CreateProcessor(db1, gateway);
        var worker2 = CreateProcessor(db2, gateway);

        await Task.WhenAll(
            worker1.ProcessOutboxBatchAsync("worker-one", 10, TestContext.Current.CancellationToken),
            worker2.ProcessOutboxBatchAsync("worker-two", 10, TestContext.Current.CancellationToken));

        await using var verification = CreateContext();
        var attempt = await verification.PaymentAttempts.SingleAsync(a => a.Id == seeded.AttemptId, TestContext.Current.CancellationToken);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(PaymentAttemptStatus.AwaitingPayment, attempt.Status);
        Assert.Equal("INV-1", attempt.GatewayInvoiceId);
        Assert.Equal(OutboxStatus.Processed, await verification.OutboxMessages.Select(m => m.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Expired_dispatch_lease_after_crash_never_resends_unknown_invoice()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.Dispatching, OutboxStatus.Processing, DateTime.UtcNow.AddMinutes(-1));
        var gateway = new StubGateway(new(GatewayDispatchOutcome.Created, "SHOULD-NOT-HAPPEN"));
        await using var db = CreateContext();
        var worker = CreateProcessor(db, gateway);

        await worker.ProcessOutboxBatchAsync("restart-worker", 10, TestContext.Current.CancellationToken);

        await using var verification = CreateContext();
        var attempt = await verification.PaymentAttempts.SingleAsync(a => a.Id == seeded.AttemptId, TestContext.Current.CancellationToken);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation, attempt.Status);
        Assert.Equal("WORKER_RESTART_AFTER_DISPATCH", attempt.LastErrorCode);
    }

    [Fact]
    public async Task Remote_timeout_enters_reconciliation_and_is_not_blindly_retried()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.PendingDispatch, OutboxStatus.Pending);
        var gateway = new StubGateway(new(GatewayDispatchOutcome.Unknown, ErrorCode: "TIMEOUT_UNKNOWN_OUTCOME"));
        await using (var db = CreateContext())
            await CreateProcessor(db, gateway).ProcessOutboxBatchAsync("timeout-worker", 10, TestContext.Current.CancellationToken);
        await using (var db = CreateContext())
            await CreateProcessor(db, gateway).ProcessOutboxBatchAsync("second-delivery", 10, TestContext.Current.CancellationToken);

        await using var verification = CreateContext();
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation,
            await verification.PaymentAttempts.Where(a => a.Id == seeded.AttemptId).Select(a => a.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reservation_expiry_releases_stock_and_coupon_exactly_once_and_late_payment_reconciles()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.AwaitingPayment, outboxStatus: null, withExpiredReservation: true);
        var gateway = new StubGateway(new(GatewayDispatchOutcome.Created));
        await using (var db = CreateContext())
        {
            var processor = CreateProcessor(db, gateway);
            Assert.Equal(1, await processor.ExpireReservationsAsync(TestContext.Current.CancellationToken));
        }
        await using (var db = CreateContext())
        {
            var processor = CreateProcessor(db, gateway);
            Assert.Equal(0, await processor.ExpireReservationsAsync(TestContext.Current.CancellationToken));
            Assert.False(await processor.FinalizeVerifiedAsync(seeded.AttemptId, "late-evidence", "INV-SEED", seeded.MerchantReference, "merchant-test", 12.345m, "KWD", TestContext.Current.CancellationToken));
        }

        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Products.Where(p => p.Id == seeded.ProductId).Select(p => p.TotalInStock).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await verification.Coupons.Select(c => c.UsedCount).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OrderStatus.RequiresReconciliation, await verification.Orders.Where(o => o.Id == seeded.OrderId).Select(o => o.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation, await verification.PaymentAttempts.Where(a => a.Id == seeded.AttemptId).Select(a => a.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Verified_payment_confirms_active_reservation_and_duplicate_evidence_is_idempotent()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.AwaitingPayment, outboxStatus: null, withActiveReservation: true);
        await using var db = CreateContext();
        var processor = CreateProcessor(db, new StubGateway(new(GatewayDispatchOutcome.Created)));
        Assert.True(await processor.FinalizeVerifiedAsync(seeded.AttemptId, "evidence-1", "INV-SEED", seeded.MerchantReference, "merchant-test", 12.345m, "KWD", TestContext.Current.CancellationToken));
        Assert.True(await processor.FinalizeVerifiedAsync(seeded.AttemptId, "evidence-1", "INV-SEED", seeded.MerchantReference, "merchant-test", 12.345m, "KWD", TestContext.Current.CancellationToken));

        await using var verification = CreateContext();
        Assert.Equal(TransactionStatus.Paid, await verification.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OrderStatus.Processing, await verification.Orders.Select(o => o.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ReservationStatus.Confirmed, await verification.InventoryReservations.Select(r => r.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PaymentTransitionAudits.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Email_failure_is_persisted_and_a_later_worker_retry_completes_it()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.PendingDispatch, outboxStatus: null);
        await using (var seed = CreateContext())
        {
            seed.OutboxMessages.Add(new OutboxMessage { Type = "OrderCreatedEmail", DeduplicationKey = $"order:{seeded.OrderId}:email", Payload = JsonSerializer.Serialize(new { OrderId = seeded.OrderId }), NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1) });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var sends = 0;
        var email = InterfaceStub.Create<IEmailService>((method, _) => method.Name == "SendOrderNotificationToAdminAsync"
            ? Task.FromResult(Interlocked.Increment(ref sends) > 1)
            : throw new InvalidOperationException(method.Name));
        await using (var firstDb = CreateContext())
            await CreateProcessor(firstDb, new StubGateway(new(GatewayDispatchOutcome.Created)), email).ProcessOutboxBatchAsync("email-worker-1", 10, TestContext.Current.CancellationToken);
        await using (var reset = CreateContext())
            await reset.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);
        await using (var secondDb = CreateContext())
            await CreateProcessor(secondDb, new StubGateway(new(GatewayDispatchOutcome.Created)), email).ProcessOutboxBatchAsync("email-worker-2", 10, TestContext.Current.CancellationToken);

        await using var verification = CreateContext();
        Assert.Equal(2, sends);
        Assert.Equal(OutboxStatus.Processed, await verification.OutboxMessages.Select(m => m.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    private PaymentOrchestrationService CreateProcessor(EcomDbContext db, ISadadInvoiceGateway gateway, IEmailService? emailOverride = null)
    {
        var email = InterfaceStub.Create<IEmailService>((method, _) => method.Name == "SendOrderNotificationToAdminAsync"
            ? Task.FromResult(true)
            : throw new InvalidOperationException(method.Name));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:AdminEmail"] = "admin@example.test",
            ["Sadad:MerchantIdentity"] = "merchant-test",
            ["Sadad:Verification:ContractProfileCertified"] = "true",
            ["Sadad:Verification:EnablePaidTransitions"] = "true"
        }).Build();
        return new PaymentOrchestrationService(db, gateway, emailOverride ?? email, TestMapper.Create(), configuration, NullLogger<PaymentOrchestrationService>.Instance);
    }

    private EcomDbContext CreateContext() => new(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(ConnectionString).Options);

    private async Task<(int OrderId, int AttemptId, int ProductId, string MerchantReference)> SeedAttemptAsync(
        PaymentAttemptStatus status, OutboxStatus? outboxStatus, DateTime? leaseUntil = null,
        bool withExpiredReservation = false, bool withActiveReservation = false)
    {
        await using var db = CreateContext();
        var category = new Category { Name = $"C-{Guid.NewGuid():N}", NameAr = "فئة" };
        var sub = new SubCategory { Name = $"S-{Guid.NewGuid():N}", NameAr = "فرع", Category = category };
        var product = new Product { Title = "P", TitleAr = "م", Description = "D", DescriptionAr = "و", subCategory = sub, oldPrice = 12.345m, newPrice = 12.345m, TotalInStock = withExpiredReservation || withActiveReservation ? 0 : 1, IsInStock = true };
        var coupon = new Coupon { Code = $"CP-{Guid.NewGuid():N}", Description = "D", Type = CouponType.FixedAmount, Value = 1m, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1), UsageLimit = 1, UsedCount = withExpiredReservation || withActiveReservation ? 1 : 0 };
        var order = new Order { OrderNumber = $"O-{Guid.NewGuid():N}", ShippingAddress = new ShippingAddress { FullName = "G", Phone = "1", Street = "S" }, Subtotal = 12.345m, Total = 12.345m, Status = OrderStatus.Pending, Coupon = coupon, ReservationExpiresAtUtc = withExpiredReservation ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(30) };
        var transaction = new Transaction { Order = order, Amount = 12.345m, PaymentMethod = PaymentMethod.Bank, Status = TransactionStatus.Pending, TransactionReference = $"T-{Guid.NewGuid():N}" };
        db.AddRange(category, sub, product, coupon, order, transaction);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var attempt = new PaymentAttempt { OrderId = order.Id, TransactionId = transaction.Id, IdempotencyKeyHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadRight(64, '0'), RequestHash = new string('A', 64), MerchantReference = $"PAY-{Guid.NewGuid():N}", Amount = 12.345m, Currency = "KWD", Status = status, GatewayInvoiceId = "INV-SEED" };
        db.PaymentAttempts.Add(attempt);
        if (withExpiredReservation || withActiveReservation)
        {
            var expiry = withExpiredReservation ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(30);
            db.InventoryReservations.Add(new InventoryReservation { OrderId = order.Id, ProductId = product.Id, Quantity = 1, Status = ReservationStatus.Active, ExpiresAtUtc = expiry });
            db.CouponReservations.Add(new CouponReservation { OrderId = order.Id, CouponId = coupon.Id, Status = ReservationStatus.Active, ExpiresAtUtc = expiry });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (outboxStatus.HasValue)
        {
            db.OutboxMessages.Add(new OutboxMessage { Type = "PaymentInvoiceRequested", DeduplicationKey = $"attempt:{attempt.Id}", Payload = JsonSerializer.Serialize(new { PaymentAttemptId = attempt.Id }), Status = outboxStatus.Value, LeaseOwner = outboxStatus == OutboxStatus.Processing ? "dead-worker" : null, LeaseUntilUtc = leaseUntil, NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1) });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return (order.Id, attempt.Id, product.Id, attempt.MerchantReference);
    }

    private sealed class StubGateway(GatewayDispatchResult result) : ISadadInvoiceGateway
    {
        private int _calls;
        public int Calls => _calls;
        public Task<GatewayDispatchResult> CreateInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(result);
        }

        public Task<SadadInvoiceVerificationResult> VerifyInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken) =>
            Task.FromResult(new SadadInvoiceVerificationResult(SadadInvoiceVerificationOutcome.NotPaid, "TEST_NOT_PAID"));

        public Task<SadadInvoiceLookupResult> FindInvoiceByReferenceAsync(PaymentAttempt attempt, CancellationToken cancellationToken) =>
            Task.FromResult(new SadadInvoiceLookupResult(SadadInvoiceLookupOutcome.NotFound, "TEST_NOT_FOUND"));
    }
}
