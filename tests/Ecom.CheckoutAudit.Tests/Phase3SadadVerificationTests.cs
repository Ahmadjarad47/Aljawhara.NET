using System.Net;
using System.Text.Json;
using AutoMapper;
using Ecom.Application.Payments;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

public sealed class Phase3SadadVerificationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"EcomPhase3_{Guid.NewGuid():N}";
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
    public async Task Unpaid_invoice_is_validated_but_never_finalized()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var (gateway, handler) = CreateRealGateway(db, InvoiceBody(seeded.Reference, status: "Unpaid", includeTransaction: false));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.NotPaid, result.Outcome);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Strict_paid_invoice_captures_three_decimal_evidence()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var (gateway, _) = CreateRealGateway(db, InvoiceBody(seeded.Reference));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.VerifiedPaid, result.Outcome);
        Assert.NotNull(result.Payment);
        Assert.Equal(12.345m, result.Payment.Amount);
        Assert.Equal("TX-9001", result.Payment.GatewayTransactionId);
        Assert.Equal(64, result.Payment.EvidenceId.Length);
    }

    public static TheoryData<string, string> RejectedResponses => new()
    {
        { InvoiceBody("REF", invoiceId: 9002), "INVOICE_ID_MISMATCH" },
        { InvoiceBody("REF", vendorId: 52), "VENDOR_ID_MISMATCH" },
        { InvoiceBody("OTHER"), "MERCHANT_REFERENCE_MISMATCH" },
        { InvoiceBody("REF", amount: 12.344m), "AMOUNT_FIELDS_MISMATCH" },
        { InvoiceBody("REF", currency: "USD"), "CURRENCY_MISMATCH" },
        { InvoiceBody("REF", status: "Processing"), "UNKNOWN_PAYMENT_STATUS" },
        { InvoiceBody("REF", includeTransaction: false), "MISSING_PAYMENT_TRANSACTION_EVIDENCE" },
        { InvoiceBody("REF", refundedAmount: 1m), "REFUND_OR_PARTIAL_REFUND_REQUIRES_RECONCILIATION" },
        { InvoiceBody("REF", amount: 12.3456m), "KWD_SCALE_INVALID" },
        { "{\"isValid\":true,\"response\":{\"id\":9001}}", "MISSING_REQUIRED_FIELD" }
    };

    [Theory]
    [MemberData(nameof(RejectedResponses))]
    public async Task Invalid_incomplete_refunded_or_mismatched_responses_fail_closed(string responseBody, string reason)
    {
        var seeded = await SeedAttemptAsync(reference: "REF");
        await using var db = CreateContext();
        var (gateway, _) = CreateRealGateway(db, responseBody);

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(reason, result.ReasonCode);
        Assert.NotEqual(SadadInvoiceVerificationOutcome.VerifiedPaid, result.Outcome);
    }

    [Fact]
    public async Task Discounted_invoice_remains_blocked_until_amount_semantics_are_certified()
    {
        var seeded = await SeedAttemptAsync(couponDiscount: 1m);
        await using var db = CreateContext();
        var (gateway, handler) = CreateRealGateway(db, InvoiceBody(seeded.Reference));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.Unsupported, result.Outcome);
        Assert.Equal("DISCOUNT_SEMANTICS_NOT_CERTIFIED", result.ReasonCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Duplicate_webhook_and_replay_finalize_once()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        var handler = new SadadHandler((_, _) => SadadHandler.Json(InvoiceBody(seeded.Reference)));

        await using (var firstDb = CreateContext())
        {
            var service = CreateProcessor(firstDb, CreateRealGateway(firstDb, handler).Gateway);
            var first = await service.VerifyByInvoiceIdAsync("9001", "webhook:first", TestContext.Current.CancellationToken);
            Assert.Equal(PaymentVerificationOutcome.Paid, first.Outcome);
        }
        await using (var secondDb = CreateContext())
        {
            var service = CreateProcessor(secondDb, CreateRealGateway(secondDb, handler).Gateway);
            var replay = await service.VerifyByInvoiceIdAsync("9001", "webhook:replay", TestContext.Current.CancellationToken);
            Assert.Equal(PaymentVerificationOutcome.AlreadyPaid, replay.Outcome);
        }

        await using var verification = CreateContext();
        Assert.Equal(1, handler.Calls);
        Assert.Equal(TransactionStatus.Paid, await verification.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PaymentTransitionAudits.Where(a => a.ToStatus == PaymentAttemptStatus.Paid).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_verifiers_call_GetById_once_and_finalize_once()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        var handler = new SadadHandler((_, _) =>
        {
            Thread.Sleep(75);
            return SadadHandler.Json(InvoiceBody(seeded.Reference));
        });
        var contexts = Enumerable.Range(0, 8).Select(_ => CreateContext()).ToList();
        try
        {
            var tasks = contexts.Select((db, index) =>
            {
                var service = CreateProcessor(db, CreateRealGateway(db, handler).Gateway);
                return service.VerifyByInvoiceIdAsync("9001", $"worker:{index}", TestContext.Current.CancellationToken);
            });
            var results = await Task.WhenAll(tasks);

            Assert.Single(results, r => r.Outcome == PaymentVerificationOutcome.Paid);
            Assert.All(results.Where(r => r.Outcome != PaymentVerificationOutcome.Paid),
                r => Assert.True(r.Outcome is PaymentVerificationOutcome.Busy or PaymentVerificationOutcome.AlreadyPaid));
            Assert.Equal(1, handler.Calls);
        }
        finally
        {
            foreach (var db in contexts) await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Paid_result_after_reservation_expiry_is_reconciled_not_fulfilled()
    {
        var seeded = await SeedAttemptAsync(withExpiredReservation: true);
        await using var db = CreateContext();
        var service = CreateProcessor(db, new ScriptedGateway(
            verification: new(SadadInvoiceVerificationOutcome.VerifiedPaid, "STRICT_GETBYID_VERIFIED",
                Verified(seeded.Reference))));

        var result = await service.VerifyByInvoiceIdAsync("9001", "late-callback", TestContext.Current.CancellationToken);

        Assert.Equal(PaymentVerificationOutcome.RequiresReconciliation, result.Outcome);
        await using var verification = CreateContext();
        Assert.Equal(TransactionStatus.Pending, await verification.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation, await verification.PaymentAttempts.Select(a => a.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unknown_creation_is_recovered_only_from_one_exact_GetAll_match_then_reverified()
    {
        var seeded = await SeedAttemptAsync(PaymentAttemptStatus.RequiresReconciliation, gatewayInvoiceId: null, withActiveReservation: true);
        await using var db = CreateContext();
        var gateway = new ScriptedGateway(
            verification: new(SadadInvoiceVerificationOutcome.VerifiedPaid, "STRICT_GETBYID_VERIFIED", Verified(seeded.Reference)),
            lookup: new(SadadInvoiceLookupOutcome.FoundUnique, "GETALL_ONE_EXACT_MATCH", "9001"));
        var service = CreateProcessor(db, gateway);

        var processed = await service.VerifyPendingBatchAsync("reconcile-worker", 10, TestContext.Current.CancellationToken);

        Assert.Equal(1, processed);
        await using var verification = CreateContext();
        var attempt = await verification.PaymentAttempts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("9001", attempt.GatewayInvoiceId);
        Assert.Equal(PaymentAttemptStatus.Paid, attempt.Status);
        Assert.Equal(1, gateway.LookupCalls);
        Assert.Equal(1, gateway.VerificationCalls);
    }

    [Fact]
    public async Task Ambiguous_GetAll_result_preserves_reconciliation_and_never_recreates_invoice()
    {
        await SeedAttemptAsync(PaymentAttemptStatus.RequiresReconciliation, gatewayInvoiceId: null);
        await using var db = CreateContext();
        var gateway = new ScriptedGateway(
            verification: new(SadadInvoiceVerificationOutcome.NotPaid, "SHOULD_NOT_RUN"),
            lookup: new(SadadInvoiceLookupOutcome.Ambiguous, "GETALL_MULTIPLE_EXACT_MATCHES"));
        var service = CreateProcessor(db, gateway);

        var processed = await service.VerifyPendingBatchAsync("reconcile-worker", 10, TestContext.Current.CancellationToken);

        Assert.Equal(0, processed);
        Assert.Equal(0, gateway.CreateCalls);
        Assert.Equal(0, gateway.VerificationCalls);
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation,
            await db.PaymentAttempts.Select(a => a.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    private EcomDbContext CreateContext() => new(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(ConnectionString).Options);

    private IConfiguration Configuration(bool getAllCertified = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Notifications:AdminEmail"] = "admin@example.test",
        ["Sadad:MerchantVendorId"] = "51",
        ["Sadad:Verification:ContractProfileCertified"] = "true",
        ["Sadad:Verification:EnablePaidTransitions"] = "true",
        ["Sadad:Verification:GetAllContractCertified"] = getAllCertified.ToString()
    }).Build();

    private (SadadInvoiceGateway Gateway, SadadHandler Handler) CreateRealGateway(EcomDbContext db, string body)
    {
        var handler = new SadadHandler((_, _) => SadadHandler.Json(body));
        return CreateRealGateway(db, handler);
    }

    private (SadadInvoiceGateway Gateway, SadadHandler Handler) CreateRealGateway(EcomDbContext db, SadadHandler handler)
    {
        var tokenService = InterfaceStub.Create<ITransactionService>((method, _) => method.Name == "GenerateTokens"
            ? Task.FromResult(new TokenResponse { AccessToken = "access-test", RefreshToken = "refresh-test", ExpiredAfterSeconds = 3600 })
            : throw new InvalidOperationException(method.Name));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        return (new SadadInvoiceGateway(db, tokenService, new StubHttpClientFactory(client), Configuration(), NullLogger<SadadInvoiceGateway>.Instance), handler);
    }

    private PaymentOrchestrationService CreateProcessor(EcomDbContext db, ISadadInvoiceGateway gateway)
    {
        var email = InterfaceStub.Create<IEmailService>((method, _) => method.Name == "SendOrderNotificationToAdminAsync"
            ? Task.FromResult(true)
            : throw new InvalidOperationException(method.Name));
        return new PaymentOrchestrationService(db, gateway, email, TestMapper.Create(), Configuration(), NullLogger<PaymentOrchestrationService>.Instance);
    }

    private async Task<(int AttemptId, string Reference)> SeedAttemptAsync(
        PaymentAttemptStatus status = PaymentAttemptStatus.AwaitingPayment,
        string? reference = null,
        string? gatewayInvoiceId = "9001",
        decimal? couponDiscount = null,
        bool withActiveReservation = false,
        bool withExpiredReservation = false)
    {
        await using var db = CreateContext();
        var category = new Category { Name = $"C-{Guid.NewGuid():N}", NameAr = "فئة" };
        var sub = new SubCategory { Name = $"S-{Guid.NewGuid():N}", NameAr = "فرع", Category = category };
        var product = new Product { Title = "P", TitleAr = "م", Description = "D", DescriptionAr = "و", subCategory = sub, oldPrice = 12.345m, newPrice = 12.345m, TotalInStock = 0, IsInStock = true };
        var order = new Order
        {
            OrderNumber = $"O-{Guid.NewGuid():N}",
            ShippingAddress = new ShippingAddress { FullName = "G", Phone = "1", Street = "S" },
            Subtotal = 12.345m,
            Total = 12.345m,
            CouponDiscountAmount = couponDiscount,
            Status = OrderStatus.Pending,
            ReservationExpiresAtUtc = withExpiredReservation ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(30)
        };
        var transaction = new Transaction { Order = order, Amount = 12.345m, PaymentMethod = PaymentMethod.Bank, Status = TransactionStatus.Pending, TransactionReference = $"LOCAL-{Guid.NewGuid():N}" };
        db.AddRange(category, sub, product, order, transaction);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var merchantReference = reference ?? "REF";
        var attempt = new PaymentAttempt
        {
            OrderId = order.Id,
            TransactionId = transaction.Id,
            IdempotencyKeyHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadRight(64, '0'),
            RequestHash = new string('A', 64),
            MerchantReference = merchantReference,
            Amount = 12.345m,
            Currency = "KWD",
            Status = status,
            GatewayInvoiceId = gatewayInvoiceId
        };
        db.PaymentAttempts.Add(attempt);
        if (withActiveReservation || withExpiredReservation)
        {
            db.InventoryReservations.Add(new InventoryReservation
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = 1,
                Status = ReservationStatus.Active,
                ExpiresAtUtc = withExpiredReservation ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(30)
            });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (attempt.Id, merchantReference);
    }

    private static string InvoiceBody(
        string reference,
        long invoiceId = 9001,
        long vendorId = 51,
        string status = "Paid",
        decimal amount = 12.345m,
        string currency = "KWD",
        decimal refundedAmount = 0m,
        bool includeTransaction = true) => JsonSerializer.Serialize(new
        {
            isValid = true,
            errorKey = (string?)null,
            response = new
            {
                id = invoiceId,
                vendor_Id = vendorId,
                status,
                amount,
                total = amount,
                subtotal = amount,
                refunded_Amount = refundedAmount,
                ref_Number = reference,
                currency_Code = currency,
                transactions = includeTransaction ? new object[] { new { evidence = "present" } } : [],
                transactionID = includeTransaction ? "TX-9001" : null
            }
        });

    private static SadadVerifiedPayment Verified(string reference) =>
        new(new string('E', 64), "9001", 51, reference, 12.345m, 12.345m, 12.345m, 0m, "KWD", "TX-9001");

    private sealed class ScriptedGateway(
        SadadInvoiceVerificationResult verification,
        SadadInvoiceLookupResult? lookup = null) : ISadadInvoiceGateway
    {
        private int _createCalls;
        private int _verificationCalls;
        private int _lookupCalls;
        public int CreateCalls => _createCalls;
        public int VerificationCalls => _verificationCalls;
        public int LookupCalls => _lookupCalls;

        public Task<GatewayDispatchResult> CreateInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _createCalls);
            return Task.FromResult(new GatewayDispatchResult(GatewayDispatchOutcome.Unknown));
        }

        public Task<SadadInvoiceVerificationResult> VerifyInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _verificationCalls);
            return Task.FromResult(verification);
        }

        public Task<SadadInvoiceLookupResult> FindInvoiceByReferenceAsync(PaymentAttempt attempt, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _lookupCalls);
            return Task.FromResult(lookup ?? new(SadadInvoiceLookupOutcome.NotFound, "NOT_FOUND"));
        }
    }
}
