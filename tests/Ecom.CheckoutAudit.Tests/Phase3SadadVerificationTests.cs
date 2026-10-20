using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using Ecom.Application.Payments;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

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
    public async Task Verification_precondition_failure_keeps_the_existing_invoice_awaiting_payment()
    {
        await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        await db.PaymentAttempts.ExecuteUpdateAsync(s => s.SetProperty(a => a.PaymentUrl,
            "https://sadad.test/pay/9001"), TestContext.Current.CancellationToken);
        var gateway = new ScriptedGateway(new(SadadInvoiceVerificationOutcome.Unsupported, "MERCHANT_VENDOR_ID_NOT_CONFIGURED"));

        await CreateProcessor(db, gateway).VerifyPendingBatchAsync("background-check", 10, TestContext.Current.CancellationToken);

        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentAttemptStatus.AwaitingPayment, attempt.Status);
        Assert.Equal("https://sadad.test/pay/9001", attempt.PaymentUrl);
        Assert.Equal("MERCHANT_VENDOR_ID_NOT_CONFIGURED", attempt.LastErrorCode);
        Assert.Null(attempt.LeaseOwner);
        Assert.Null(attempt.LeaseUntilUtc);
        Assert.Equal(TransactionStatus.Pending, await db.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Status_poll_recovers_paid_invoice_without_a_webhook_and_throttles_unpaid_checks()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        var paid = false;
        var handler = new SadadHandler((_, _) => SadadHandler.Json(InvoiceBody(seeded.Reference,
            status: paid ? "Paid" : "Unpaid", includeTransaction: paid)));
        int orderId;
        await using (var db = CreateContext())
        {
            orderId = await db.PaymentAttempts.Select(a => a.OrderId).SingleAsync(TestContext.Current.CancellationToken);
            var service = CreateProcessor(db, CreateRealGateway(db, handler).Gateway);
            Assert.Equal(PaymentVerificationOutcome.NotPaid,
                (await service.VerifyByOrderIdAsync(orderId, TestContext.Current.CancellationToken)).Outcome);
        }
        paid = true;
        await using (var db = CreateContext())
        {
            var service = CreateProcessor(db, CreateRealGateway(db, handler).Gateway);
            Assert.Equal(PaymentVerificationOutcome.Busy,
                (await service.VerifyByOrderIdAsync(orderId, TestContext.Current.CancellationToken)).Outcome);
            Assert.Equal(1, handler.Calls);
            await db.PaymentAttempts.ExecuteUpdateAsync(s => s.SetProperty(a => a.LastVerificationAtUtc,
                DateTime.UtcNow.AddSeconds(-11)), TestContext.Current.CancellationToken);
        }
        await using (var db = CreateContext())
        {
            var service = CreateProcessor(db, CreateRealGateway(db, handler).Gateway);
            Assert.Equal(PaymentVerificationOutcome.Paid,
                (await service.VerifyByOrderIdAsync(orderId, TestContext.Current.CancellationToken)).Outcome);
            Assert.Equal(PaymentVerificationOutcome.AlreadyPaid,
                (await service.VerifyByOrderIdAsync(orderId, TestContext.Current.CancellationToken)).Outcome);
        }
        await using var verification = CreateContext();
        Assert.Equal(2, handler.Calls);
        Assert.Equal(TransactionStatus.Paid, await verification.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ReservationStatus.Confirmed, await verification.InventoryReservations.Select(r => r.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PaymentTransitionAudits.Where(a => a.ToStatus == PaymentAttemptStatus.Paid).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Webhook_rechecks_immediately_even_after_a_recent_unpaid_status_poll()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        await db.PaymentAttempts.ExecuteUpdateAsync(s => s.SetProperty(a => a.LastVerificationAtUtc, DateTime.UtcNow), TestContext.Current.CancellationToken);
        var (gateway, handler) = CreateRealGateway(db, InvoiceBody(seeded.Reference));

        var result = await CreateProcessor(db, gateway).VerifyByInvoiceIdAsync("9001", "webhook:paid", TestContext.Current.CancellationToken);

        Assert.Equal(PaymentVerificationOutcome.Paid, result.Outcome);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Concurrent_status_polls_share_the_persistent_verification_lease()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        var handler = new SadadHandler((_, _) => SadadHandler.Json(InvoiceBody(seeded.Reference, status: "Unpaid", includeTransaction: false)));
        await using var lookup = CreateContext();
        var orderId = await lookup.PaymentAttempts.Select(a => a.OrderId).SingleAsync(TestContext.Current.CancellationToken);
        var contexts = Enumerable.Range(0, 8).Select(_ => CreateContext()).ToList();
        try
        {
            var results = await Task.WhenAll(contexts.Select(db => CreateProcessor(db, CreateRealGateway(db, handler).Gateway)
                .VerifyByOrderIdAsync(orderId, TestContext.Current.CancellationToken)));
            Assert.Single(results, r => r.Outcome == PaymentVerificationOutcome.NotPaid);
            Assert.Equal(7, results.Count(r => r.Outcome == PaymentVerificationOutcome.Busy));
            Assert.Equal(1, handler.Calls);
        }
        finally
        {
            foreach (var db in contexts) await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Disabled_paid_transitions_persist_a_diagnostic_reason_instead_of_marking_paid()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        var config = new ConfigurationBuilder().AddConfiguration(Configuration())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sadad:Verification:EnablePaidTransitions"] = "false" }).Build();
        var email = InterfaceStub.Create<IEmailService>((method, _) => throw new InvalidOperationException(method.Name));
        var processor = new PaymentOrchestrationService(db, CreateRealGateway(db, InvoiceBody(seeded.Reference)).Gateway,
            email, TestMapper.Create(), config, NullLogger<PaymentOrchestrationService>.Instance);

        var result = await processor.VerifyByInvoiceIdAsync("9001", "callback:paid", TestContext.Current.CancellationToken);

        Assert.Equal("PAID_TRANSITIONS_NOT_CERTIFIED", result.ReasonCode);
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.ReasonCode, attempt.LastErrorCode);
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation, attempt.Status);
        Assert.NotNull(attempt.LastVerificationAtUtc);
        Assert.Null(attempt.LeaseOwner);
        Assert.Equal(TransactionStatus.Pending, await db.Transactions.Select(t => t.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejected_finalization_releases_the_lease_and_persists_the_failure()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        var payment = Verified(seeded.Reference) with { Amount = 99m };
        var service = CreateProcessor(db, new ScriptedGateway(new(SadadInvoiceVerificationOutcome.VerifiedPaid,
            "STRICT_GETBYID_VERIFIED", payment)));

        var result = await service.VerifyByInvoiceIdAsync("9001", "callback:invalid-finalization", TestContext.Current.CancellationToken);

        Assert.Equal("FINALIZATION_REJECTED", result.ReasonCode);
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("FINALIZATION_REJECTED", attempt.LastErrorCode);
        Assert.Equal(PaymentAttemptStatus.RequiresReconciliation, attempt.Status);
        Assert.Null(attempt.LeaseOwner);
        Assert.Null(attempt.LeaseUntilUtc);
        Assert.NotNull(attempt.LastVerificationAtUtc);
    }

    [Fact]
    public async Task Cancelled_verification_releases_the_lease_without_waiting_for_its_expiry()
    {
        await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var gateway = InterfaceStub.Create<ISadadInvoiceGateway>((method, _) =>
        {
            Assert.Equal("VerifyInvoiceAsync", method.Name);
            cancellation.Cancel();
            return Task.FromCanceled<SadadInvoiceVerificationResult>(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateProcessor(db, gateway)
            .VerifyByInvoiceIdAsync("9001", "callback:cancelled", cancellation.Token));

        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(attempt.LeaseOwner);
        Assert.Null(attempt.LeaseUntilUtc);
        Assert.NotNull(attempt.LastVerificationAtUtc);
        Assert.Equal("VERIFICATION_UNAVAILABLE", attempt.LastErrorCode);
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

    [Fact]
    public async Task Paid_invoice_accepts_a_different_subtotal_and_preserves_it_for_audit()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var (gateway, _) = CreateRealGateway(db, InvoiceBody(seeded.Reference, subtotal: 11.111m));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.VerifiedPaid, result.Outcome);
        Assert.Equal(11.111m, result.Payment!.Subtotal);
    }

    [Fact]
    public async Task Different_subtotal_flows_through_atomic_finalization_without_blocking_payment()
    {
        var seeded = await SeedAttemptAsync(withActiveReservation: true);
        await using var db = CreateContext();
        var gateway = CreateRealGateway(db, InvoiceBody(seeded.Reference, subtotal: 11.111m)).Gateway;

        var result = await CreateProcessor(db, gateway)
            .VerifyByInvoiceIdAsync("9001", "regression:subtotal", TestContext.Current.CancellationToken);

        Assert.Equal(PaymentVerificationOutcome.Paid, result.Outcome);
        await using var verification = CreateContext();
        var attempt = await verification.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PaymentAttemptStatus.Paid, attempt.Status);
        Assert.Equal(11.111m, attempt.VerifiedSubtotal);
        Assert.Equal(TransactionStatus.Paid,
            await verification.Transactions.Select(transaction => transaction.Status).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OrderStatus.Processing,
            await verification.Orders.Select(order => order.Status).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Paid_invoice_normalizes_binary_floating_point_noise_at_Kwd_precision()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var attempt = await db.PaymentAttempts.SingleAsync(TestContext.Current.CancellationToken);
        attempt.Amount = 354.669m;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (gateway, _) = CreateRealGateway(db, InvoiceBody(
            seeded.Reference,
            amount: 354.66900000000004m,
            total: 354.66900000000004m,
            subtotal: 340.482m,
            transactionAmount: 354.66900000000004m));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.VerifiedPaid, result.Outcome);
        Assert.Equal(354.669m, result.Payment!.Amount);
        Assert.Equal(354.669m, result.Payment.Total);
        Assert.Equal(340.482m, result.Payment.Subtotal);
    }

    [Fact]
    public async Task Invoice_diagnostics_are_disabled_when_the_configuration_flag_is_absent()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var logger = new RecordingGatewayLogger();
        var handler = new SadadHandler((_, _) => SadadHandler.Json(InvoiceBody(seeded.Reference)));
        var (gateway, _) = CreateRealGateway(db, handler, logger);

        await gateway.VerifyInvoiceAsync(await db.PaymentAttempts.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(logger.Messages, message => message.Contains("[Sadad GETBYID AMOUNTS]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enabled_invoice_diagnostics_log_only_serialized_financial_fields()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var order = await db.Orders.SingleAsync(TestContext.Current.CancellationToken);
        var attempt = await db.PaymentAttempts.SingleAsync(TestContext.Current.CancellationToken);
        var product = await db.Products.SingleAsync(TestContext.Current.CancellationToken);
        order.Subtotal = 195.541m;
        order.Shipping = 8.148m;
        order.Tax = 0m;
        order.Total = 203.689m;
        attempt.Amount = 203.689m;
        db.Set<OrderItem>().Add(new OrderItem
        {
            Order = order,
            Product = product,
            Name = "Private product name",
            Image = "https://private.example.test/product.jpg",
            Price = 195.541m,
            Quantity = 1
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string privateEmail = "private-customer@example.test";
        const string privateUrl = "https://sadad.test/pay/private-link";
        var handler = new SadadHandler((request, _) => SadadHandler.Json(request.RequestUri!.AbsolutePath switch
        {
            "/api/Invoice/insert" => "{\"isValid\":true,\"response\":{\"invoiceId\":\"9001\",\"customer_Email\":\"private-customer@example.test\"}}",
            "/api/Invoice/getbyid" => "{\"isValid\":true,\"response\":{\"url\":\"https://sadad.test/pay/private-link\",\"amount\":203.689,\"total\":203.689,\"subtotal\":195.541,\"discount_Amount\":0.000,\"service_Fee\":8.148,\"customer_Email\":\"private-customer@example.test\"}}",
            _ => throw new InvalidOperationException(request.RequestUri.AbsolutePath)
        }));
        var logger = new RecordingGatewayLogger();
        var diagnostics = new ConfigurationBuilder().AddConfiguration(Configuration()).AddInMemoryCollection(
            new Dictionary<string, string?> { ["Sadad:Diagnostics:EnableInvoiceLogging"] = "true" }).Build();
        var (gateway, _) = CreateRealGateway(db, handler, logger, diagnostics);

        var result = await gateway.CreateInvoiceAsync(attempt, TestContext.Current.CancellationToken);

        Assert.Equal(GatewayDispatchOutcome.Created, result.Outcome);
        Assert.Contains(logger.Messages, message => message.StartsWith("[Sadad CREATE REQUEST]", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.StartsWith("[Sadad CREATE RESPONSE]", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.StartsWith("[Sadad GETBYID AMOUNTS]", StringComparison.Ordinal));
        Assert.Equal(203.689m, logger.Properties["InvoiceAmount"]);
        Assert.Equal(203.689m, logger.Properties["InvoiceItemsTotal"]);
        Assert.Equal(203.689m, logger.Properties["GatewayAmount"]);
        Assert.Equal(203.689m, logger.Properties["GatewayTotal"]);
        Assert.Equal(195.541m, logger.Properties["GatewaySubtotal"]);
        using (var financial = JsonDocument.Parse(Assert.IsType<string>(logger.Properties["SerializedFinancialValues"])))
        {
            Assert.Equal(203.689m, financial.RootElement.GetProperty("amount").GetDecimal());
            Assert.Equal(2, financial.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(195.541m, financial.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal());
            Assert.Equal(8.148m, financial.RootElement.GetProperty("items")[1].GetProperty("amount").GetDecimal());
        }
        var logged = string.Join('\n', logger.Messages.Concat(logger.Properties.Values.Select(value => value?.ToString() ?? "")));
        Assert.DoesNotContain(privateEmail, logged, StringComparison.Ordinal);
        Assert.DoesNotContain(privateUrl, logged, StringComparison.Ordinal);
        Assert.DoesNotContain("Private product name", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("access-test", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-test", logged, StringComparison.Ordinal);
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
        { InvoiceBody("REF", amount: 12.346m), "AMOUNT_FIELDS_MISMATCH" },
        { InvoiceBody("REF", transactionAmount: 12.346m), "CAPTURE_AMOUNT_MISMATCH" },
        { InvoiceBody("REF", transactionCurrency: "USD"), "CAPTURE_CURRENCY_MISMATCH" },
        { InvoiceBody("REF", transactionInvoiceId: "9002"), "CAPTURE_INVOICE_ID_MISMATCH" },
        { InvoiceBody("REF", includeRefundTransaction: true), "SUCCESSFUL_REFUND_REQUIRES_RECONCILIATION" },
        { "{\"isValid\":true,\"response\":{\"id\":9001}}", "MISSING_REQUIRED_FIELD" }
    };

    [Theory]
    [InlineData("amount")]
    [InlineData("total")]
    public async Task Amount_mismatch_logs_the_numeric_comparison_without_customer_data(string field)
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var body = JsonNode.Parse(InvoiceBody(seeded.Reference))!;
        body["response"]![field] = 12.344m;
        body["response"]!["customer_Email"] = "private-customer@example.test";
        var logger = new RecordingGatewayLogger();
        var handler = new SadadHandler((_, _) => SadadHandler.Json(body.ToJsonString()));
        var (gateway, _) = CreateRealGateway(db, handler, logger);

        var result = await gateway.VerifyInvoiceAsync(await db.PaymentAttempts.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.Invalid, result.Outcome);
        Assert.Equal("AMOUNT_FIELDS_MISMATCH", result.ReasonCode);
        Assert.Equal(12.345m, logger.Properties["ExpectedAmount"]);
        Assert.Equal(12.345m, logger.Properties["ExpectedGrossAmount"]);
        Assert.Equal(0m, logger.Properties["ExpectedDiscountAmount"]);
        Assert.Equal(field == "amount" ? 12.344m : 12.345m, logger.Properties["GatewayAmount"]);
        Assert.Equal(field == "total" ? 12.344m : 12.345m, logger.Properties["GatewayTotal"]);
        Assert.Equal(12.345m, logger.Properties["GatewaySubtotal"]);
        Assert.DoesNotContain("private-customer", string.Join('\n', logger.Messages));
    }

    [Fact]
    public async Task Paid_invoice_with_pending_capture_requires_reconciliation()
    {
        var seeded = await SeedAttemptAsync();
        await using var db = CreateContext();
        var (gateway, _) = CreateRealGateway(db, InvoiceBody(seeded.Reference, transactionStatus: "Pending"));

        var result = await gateway.VerifyInvoiceAsync(
            await db.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(SadadInvoiceVerificationOutcome.RequiresReconciliation, result.Outcome);
        Assert.Equal("CAPTURE_TRANSACTION_NOT_SUCCESSFUL", result.ReasonCode);
    }

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
        var attempt = await verification.PaymentAttempts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(attempt.VerificationEvidenceId);
        Assert.Equal(12.345m, attempt.VerifiedAmount);
        Assert.Equal("TX-9001", attempt.GatewayTransactionId);
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

    private (SadadInvoiceGateway Gateway, SadadHandler Handler) CreateRealGateway(EcomDbContext db, SadadHandler handler,
        ILogger<SadadInvoiceGateway>? logger = null, IConfiguration? configuration = null)
    {
        var tokenService = InterfaceStub.Create<ITransactionService>((method, _) => method.Name == "GenerateTokens"
            ? Task.FromResult(new TokenResponse { AccessToken = "access-test", RefreshToken = "refresh-test", ExpiredAfterSeconds = 3600 })
            : throw new InvalidOperationException(method.Name));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        return (new SadadInvoiceGateway(db, tokenService, new StubHttpClientFactory(client), configuration ?? Configuration(), logger ?? NullLogger<SadadInvoiceGateway>.Instance), handler);
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
        decimal? total = null,
        decimal? subtotal = null,
        string currency = "KWD",
        decimal refundedAmount = 0m,
        bool includeTransaction = true,
        string transactionType = "Capture",
        string transactionStatus = "Success",
        decimal? transactionAmount = null,
        string? transactionCurrency = null,
        string? transactionInvoiceId = null,
        bool includeRefundTransaction = false)
    {
        var transactionId = "TX-9001";
        var transactionEvidence = new List<Dictionary<string, object?>>();
        if (includeTransaction)
        {
            transactionEvidence.Add(new()
            {
                ["id"] = transactionId,
                ["invoice_Id"] = transactionInvoiceId ?? invoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["type"] = transactionType,
                ["status"] = transactionStatus,
                ["amount"] = transactionAmount ?? amount,
                ["currency_Code"] = transactionCurrency ?? currency
            });
        }
        if (includeRefundTransaction)
        {
            transactionEvidence.Add(new()
            {
                ["id"] = "REFUND-9001",
                ["invoice_Id"] = invoiceId,
                ["type"] = "Refund",
                ["status"] = "Success",
                ["amount"] = 1m,
                ["currency_Code"] = currency
            });
        }

        return JsonSerializer.Serialize(new
        {
            isValid = true,
            errorKey = (string?)null,
            response = new
            {
                id = invoiceId,
                vendor_Id = vendorId,
                status,
                amount,
                total = total ?? amount,
                subtotal = subtotal ?? amount,
                refunded_Amount = refundedAmount,
                ref_Number = reference,
                currency_Code = currency,
                transactions = transactionEvidence,
                transactionID = includeTransaction ? transactionId : null
            }
        });
    }

    private static SadadVerifiedPayment Verified(string reference) =>
        new(new string('E', 64), "9001", 51, reference, 12.345m, 12.345m, 12.345m, 0m, "KWD", "TX-9001");

    private sealed class RecordingGatewayLogger : ILogger<SadadInvoiceGateway>
    {
        public Dictionary<string, object?> Properties { get; } = [];
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                foreach (var field in fields) Properties[field.Key] = field.Value;
        }
    }

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
