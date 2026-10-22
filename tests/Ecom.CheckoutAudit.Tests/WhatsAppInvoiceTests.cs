using System.Net;
using System.Text;
using System.Text.Json;
using Ecom.API.Controllers;
using Ecom.Application.WhatsApp;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecom.CheckoutAudit.Tests;

public sealed class WhatsAppInvoiceTests : IAsyncLifetime
{
    private readonly string _database = $"WhatsAppInvoice_{Guid.NewGuid():N}";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private EcomDbContext Db() => new(new DbContextOptionsBuilder<EcomDbContext>()
        .UseNpgsql(PostgresTestDatabase.ConnectionString(_database)).Options);
    public async ValueTask InitializeAsync() { await using var db = Db(); await db.Database.EnsureCreatedAsync(Ct); }
    public async ValueTask DisposeAsync() { await using var db = Db(); await db.Database.EnsureDeletedAsync(Ct); }
    private static GupshupOptions Config() => new()
    {
        Enabled = true, ApiKey = "fake-test-key", Source = "917834811114", AppName = "whatsappaljawhara",
        PublicBaseUrl = "https://api.example.test", WebhookSecret = new string('W', 64), SandboxAllowedDestinations = ["96550000000"]
    };

    [Theory]
    [InlineData("50000000", "+96550000000")]
    [InlineData("+965 5000-0000", "+96550000000")]
    [InlineData("0096550000000", "+96550000000")]
    [InlineData("96550000000", "+96550000000")]
    [InlineData("+4950000000", null)]
    [InlineData("050000000", null)]
    [InlineData("5000000", null)]
    [InlineData("50000000x", null)]
    [InlineData("965+50000000", null)]
    public void Country_prefix_normalization_and_validation(string input, string? expected) =>
        Assert.Equal(expected, WhatsAppPhone.Normalize(input, Config()));

    [Theory]
    [InlineData("+4915112345678", "KW", "+4915112345678")]
    [InlineData("004915112345678", "KW", "+4915112345678")]
    [InlineData("015112345678", "DE", "+4915112345678")]
    [InlineData("4915112345678", "KW", null)]
    [InlineData("015112345678", "KW", null)]
    [InlineData("+14155552671", "DE", "+14155552671")]
    public void International_numbers_use_explicit_prefix_or_selected_region(string input, string country, string? expected) =>
        Assert.Equal(expected, WhatsAppPhone.Normalize(input, Config(), country));

    [Fact]
    public void Country_can_be_changed_in_settings()
    {
        var o = Config(); o.CountryCode = "49"; o.NationalNumberPattern = "1[5-7][0-9]{8,9}";
        Assert.Equal("+4915207610021", WhatsAppPhone.Normalize("15207610021", o));
        Assert.Equal("+4915207610021", WhatsAppPhone.Normalize("+4915207610021", o));
        Assert.Equal("+96550000000", WhatsAppPhone.Normalize("+96550000000", o));
    }

    [Fact]
    public async Task Sandbox_sends_a_pdf_file_and_production_uses_document_template()
    {
        foreach (var production in new[] { false, true })
        {
            var o = Config();
            if (production) { o.Environment = "Production"; o.Source = "96560000000"; o.TemplateId = "approved-utility-template-id"; }
            var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"status\":\"submitted\",\"messageId\":\"GS-123\"}");
            using var client = new HttpClient(handler);
            var result = await new GupshupWhatsAppService(client, Options.Create(o))
                .SendInvoiceAsync("+96550000000", "ORDER-1", "https://api.example.test/invoice?token=test", Ct);
            Assert.Equal(WhatsAppSendOutcome.Accepted, result.Outcome);
            Assert.Equal("GS-123", result.MessageId);
            Assert.Contains(production ? "/template/msg" : "/v1/msg", handler.Url);
            var values = handler.Body!.Split('&').Select(s => s.Split('=', 2))
                .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
            Assert.Equal("96550000000", values["destination"]);
            using var json = JsonDocument.Parse(values["message"]);
            Assert.Equal(production ? "document" : "file", json.RootElement.GetProperty("type").GetString());
            var file = production ? json.RootElement.GetProperty("document") : json.RootElement;
            Assert.EndsWith(".pdf", file.GetProperty("filename").GetString());
            Assert.Equal("https://api.example.test/invoice?token=test", file.GetProperty(production ? "link" : "url").GetString());
            Assert.Equal(production, values.ContainsKey("template"));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task German_provider_wire_format_removes_plus_but_never_bypasses_sandbox(bool allowed)
    {
        var o = Config();
        o.SandboxAllowedDestinations = allowed ? ["4915112345678"] : ["96550000000"];
        var handler = new RecordingHandler(HttpStatusCode.Accepted, "{\"status\":\"submitted\",\"messageId\":\"GS-GERMAN\"}");
        using var client = new HttpClient(handler);
        var sender = new GupshupWhatsAppService(client, Options.Create(o));
        var invoice = await sender.SendInvoiceAsync("+4915112345678", "DE-ORDER", "https://api.example.test/file", Ct);
        Assert.Equal(allowed ? WhatsAppSendOutcome.Accepted : WhatsAppSendOutcome.Rejected, invoice.Outcome);
        if (allowed) Assert.Contains("destination=4915112345678", handler.Body);
        else { Assert.Null(handler.Body); Assert.Equal("SANDBOX_DESTINATION_NOT_ALLOWED", invoice.ErrorCode); }
        var verification = await sender.SendPhoneVerificationCodeAsync("+4915112345678", "123456", Ct);
        Assert.Equal(allowed ? WhatsAppSendOutcome.Accepted : WhatsAppSendOutcome.Rejected, verification.Outcome);
        if (allowed) Assert.Contains("destination=4915112345678", handler.Body);
        else Assert.Null(handler.Body);
    }

    [Theory]
    [InlineData(429, WhatsAppSendOutcome.RetryableRejection)]
    [InlineData(503, WhatsAppSendOutcome.Unknown)]
    [InlineData(401, WhatsAppSendOutcome.Rejected)]
    [InlineData(200, WhatsAppSendOutcome.Unknown)]
    public async Task Provider_errors_are_classified_without_unsafe_retries(int status, WhatsAppSendOutcome expected)
    {
        using var client = new HttpClient(new RecordingHandler((HttpStatusCode)status, "{}"));
        Assert.Equal(expected, (await new GupshupWhatsAppService(client, Options.Create(Config()))
            .SendInvoiceAsync("+96550000000", "ORDER-1", "https://api.example.test/file", Ct)).Outcome);
    }

    [Fact]
    public async Task Concurrent_workers_send_once_after_reading_committed_paid_data()
    {
        await SeedAsync();
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "GS-PAID"));
        await using var one = Db(); await using var two = Db();
        await Task.WhenAll(Processor(one, sender).ProcessBatchAsync("one", Ct), Processor(two, sender).ProcessBatchAsync("two", Ct));
        await using var check = Db();
        var invoice = await check.WhatsAppInvoices.SingleAsync(Ct);
        Assert.Equal(1, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.Accepted, invoice.Status);
        Assert.Equal("GS-PAID", invoice.ProviderMessageId);
        Assert.Equal("+96550000000", invoice.Destination);
        Assert.NotNull(invoice.Pdf);
        Assert.NotNull(invoice.MediaTokenHash);
        Assert.DoesNotContain(invoice.MediaTokenHash, sender.LastUrl!);
    }

    [Fact]
    public async Task Equivalent_phone_formats_do_not_trigger_missing_consent()
    {
        await SeedAsync(consentPhone: "+965 5000-0000");
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "GS-NORMALIZED"));
        await using var db = Db();
        await Processor(db, sender).ProcessBatchAsync("worker", Ct);
        var job = await db.WhatsAppInvoices.SingleAsync(Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Accepted, job.Status);
        Assert.Equal("+96550000000", job.Destination);
        Assert.Null(job.LastErrorCode);
    }

    [Fact]
    public async Task Revocation_after_order_consent_prevents_an_unconfirmed_send()
    {
        await SeedAsync();
        await using (var db = Db())
        {
            var order = await db.Orders.SingleAsync(Ct);
            db.WhatsAppConsentAudits.Add(new WhatsAppConsentAudit
            {
                OrderId = order.Id, Phone = "+96550000000", Action = WhatsAppConsentAction.Revoked,
                Source = "AccountSettings", OccurredAtUtc = order.WhatsAppConsentAtUtc!.Value.AddSeconds(1)
            });
            await db.SaveChangesAsync(Ct);
        }
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "NEVER"));
        await using var check = Db();
        await Processor(check, sender).ProcessBatchAsync("worker", Ct);
        Assert.Equal(0, sender.Calls);
        var job = await check.WhatsAppInvoices.SingleAsync(Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Skipped, job.Status);
        Assert.Equal("CUSTOMER_CONSENT_REVOKED", job.LastErrorCode);
    }

    [Theory]
    [InlineData(PaymentAttemptStatus.AwaitingPayment, PaymentMethod.Bank, true, "50000000", "PAYMENT_NOT_ELIGIBLE")]
    [InlineData(PaymentAttemptStatus.Failed, PaymentMethod.Bank, true, "50000000", "PAYMENT_NOT_ELIGIBLE")]
    [InlineData(PaymentAttemptStatus.Cancelled, PaymentMethod.Bank, true, "50000000", "PAYMENT_NOT_ELIGIBLE")]
    [InlineData(PaymentAttemptStatus.RequiresReconciliation, PaymentMethod.Bank, true, "50000000", "PAYMENT_NOT_ELIGIBLE")]
    [InlineData(PaymentAttemptStatus.Paid, PaymentMethod.Cod, true, "50000000", "PAYMENT_NOT_ELIGIBLE")]
    [InlineData(PaymentAttemptStatus.Paid, PaymentMethod.Bank, false, "50000000", "NO_MATCHING_CUSTOMER_CONSENT")]
    [InlineData(PaymentAttemptStatus.Paid, PaymentMethod.Bank, true, "bad-phone", "INVALID_CUSTOMER_PHONE")]
    public async Task Unsafe_or_unconsented_orders_never_send(PaymentAttemptStatus status, PaymentMethod method,
        bool consent, string phone, string reason)
    {
        await SeedAsync(status, method, consent, phone);
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "NEVER"));
        await using var db = Db();
        await Processor(db, sender).ProcessBatchAsync("worker", Ct);
        Assert.Equal(0, sender.Calls);
        var job = await db.WhatsAppInvoices.SingleAsync(Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Skipped, job.Status);
        Assert.Equal(reason, job.LastErrorCode);
        Assert.Null(job.Pdf);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task German_invoice_requires_sandbox_allowlist_and_sends_only_once(bool allowed)
    {
        await SeedAsync(phone: "+4915112345678", consentPhone: "004915112345678");
        var options = Config();
        options.SandboxAllowedDestinations = allowed ? ["4915112345678"] : ["96550000000"];
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "DE-1"));
        await using var db = Db();
        await Processor(db, sender, options).ProcessBatchAsync("german", Ct);
        await Processor(db, sender, options).ProcessBatchAsync("repeat", Ct);
        Assert.Equal(allowed ? 1 : 0, sender.Calls);
        var job = await db.WhatsAppInvoices.SingleAsync(Ct);
        Assert.Equal(allowed ? WhatsAppInvoiceStatus.Accepted : WhatsAppInvoiceStatus.Skipped, job.Status);
        if (allowed) Assert.Equal("+4915112345678", job.Destination);
        else Assert.Equal("SANDBOX_DESTINATION_NOT_ALLOWED", job.LastErrorCode);
    }

    [Fact]
    public async Task Disabled_feature_leaves_durable_jobs_pending()
    {
        await SeedAsync(); var o = Config(); o.Enabled = false;
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "NEVER"));
        await using var db = Db();
        await Processor(db, sender, o).ProcessBatchAsync("worker", Ct);
        Assert.Equal(0, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.Pending, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Explicit_rejection_retries_with_backoff_but_timeout_never_resends()
    {
        await SeedAsync();
        var sender = new FakeSender(new(WhatsAppSendOutcome.RetryableRejection, ErrorCode: "RATE_LIMIT"));
        await using var db = Db(); var processor = Processor(db, sender);
        await processor.ProcessBatchAsync("worker", Ct);
        var job = await db.WhatsAppInvoices.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Pending, job.Status);
        Assert.True(job.NextAttemptAtUtc > DateTime.UtcNow.AddSeconds(20));
        await processor.ProcessBatchAsync("worker", Ct); Assert.Equal(1, sender.Calls);
        await db.WhatsAppInvoices.ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)), Ct);
        sender.Result = new(WhatsAppSendOutcome.Unknown, ErrorCode: "TIMEOUT");
        await processor.ProcessBatchAsync("worker", Ct);
        await processor.ProcessBatchAsync("worker", Ct);
        Assert.Equal(2, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.RequiresReconciliation, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
        Assert.Equal(TransactionStatus.Paid, await db.Transactions.Select(t => t.Status).SingleAsync(Ct));
        Assert.Equal(OrderStatus.Processing, await db.Orders.Select(o => o.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Rejected_requests_stop_at_the_configured_attempt_limit()
    {
        await SeedAsync(); var config = Config(); config.MaxAttempts = 3;
        var sender = new FakeSender(new(WhatsAppSendOutcome.RetryableRejection, ErrorCode: "RATE_LIMIT"));
        await using var db = Db(); var processor = Processor(db, sender, config);
        for (var n = 0; n < 5; n++)
        {
            await db.WhatsAppInvoices.ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)), Ct);
            await processor.ProcessBatchAsync("worker", Ct);
        }
        Assert.Equal(3, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.Failed, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
        Assert.Equal(TransactionStatus.Paid, await db.Transactions.Select(t => t.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Unknown_result_can_be_linked_to_provider_evidence_without_resending()
    {
        await SeedAsync(); await using var db = Db();
        var sender = new FakeSender(new(WhatsAppSendOutcome.Unknown, ErrorCode: "TIMEOUT"));
        var processor = Processor(db, sender); await processor.ProcessBatchAsync("worker", Ct);
        var id = await db.WhatsAppInvoices.Select(j => j.Id).SingleAsync(Ct);
        var controller = Controller(db, processor);
        await controller.Webhook(Config().WebhookSecret, Event("GS-FOUND", "delivered"), Ct);
        Assert.IsType<OkResult>(await controller.Reconcile(id, new WhatsAppController.ReconcileRequest
        { ProviderMessageId = "GS-FOUND", Evidence = "Verified accepted in provider records after HTTP timeout." }, Ct));
        await processor.ProcessBatchAsync("worker", Ct);
        Assert.Equal(1, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.Delivered, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Crash_during_dispatch_is_reconciled_without_another_send()
    {
        await SeedAsync(); await using var db = Db();
        await db.WhatsAppInvoices.ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Dispatching)
            .SetProperty(j => j.LeaseUntilUtc, DateTime.UtcNow.AddMinutes(-1)), Ct);
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "NEVER"));
        await Processor(db, sender).ProcessBatchAsync("restart", Ct);
        Assert.Equal(0, sender.Calls);
        Assert.Equal(WhatsAppInvoiceStatus.RequiresReconciliation, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Callback_is_authenticated_deduplicated_and_cannot_regress_delivered()
    {
        await SeedAsync(); await using var db = Db(); var processor = Processor(db, new FakeSender(new(WhatsAppSendOutcome.Accepted, "GS-DLR")));
        await processor.ProcessBatchAsync("worker", Ct);
        var controller = Controller(db, processor);
        var delivery = Event("GS-DLR", "delivered");
        Assert.IsType<UnauthorizedResult>(await controller.Webhook("wrong", delivery, Ct));
        Assert.Empty(await db.WhatsAppDeliveryEvents.ToListAsync(Ct));
        Assert.IsType<OkResult>(await controller.Webhook(Config().WebhookSecret, delivery, Ct));
        Assert.IsType<OkResult>(await controller.Webhook(Config().WebhookSecret, delivery, Ct));
        await controller.Webhook(Config().WebhookSecret, Event("GS-DLR", "sent"), Ct);
        await controller.Webhook(Config().WebhookSecret, Event("GS-DLR", "enqueued"), Ct);
        Assert.Equal(3, await db.WhatsAppDeliveryEvents.CountAsync(Ct));
        Assert.Equal(WhatsAppInvoiceStatus.Delivered, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
        Assert.Equal(TransactionStatus.Paid, await db.Transactions.Select(t => t.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Callback_before_acceptance_response_is_replayed_after_message_id_is_saved()
    {
        await SeedAsync(); await using var db = Db();
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "GS-EARLY"));
        var processor = Processor(db, sender);
        await Controller(db, processor).Webhook(Config().WebhookSecret, Event("GS-EARLY", "read"), Ct);
        await processor.ProcessBatchAsync("worker", Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Read, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
    }

    [Fact]
    public async Task Pdf_link_requires_exact_unexpired_capability()
    {
        await SeedAsync(); await using var db = Db();
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "GS-PDF"));
        var processor = Processor(db, sender); await processor.ProcessBatchAsync("worker", Ct);
        var job = await db.WhatsAppInvoices.SingleAsync(Ct);
        var token = new Uri(sender.LastUrl!).Query.Split("token=")[1];
        var controller = Controller(db, processor);
        Assert.IsType<NotFoundResult>(await controller.Document(job.Id, null, Ct));
        Assert.IsType<NotFoundResult>(await controller.Document(job.Id, new string('0', 64), Ct));
        Assert.Equal("application/pdf", Assert.IsType<FileContentResult>(await controller.Document(job.Id, token, Ct)).ContentType);
        await db.WhatsAppInvoices.ExecuteUpdateAsync(s => s.SetProperty(j => j.MediaExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)), Ct);
        Assert.IsType<NotFoundResult>(await controller.Document(job.Id, token, Ct));
    }

    [Fact]
    public async Task Arabic_renderer_produces_searchable_paginated_pdf()
    {
        var o = Config(); o.PdfBrowserChannel = Environment.GetEnvironmentVariable("ECOM_WHATSAPP_BROWSER_CHANNEL");
        var renderer = new InvoicePdfRenderer(Options.Create(o));
        foreach (var count in new[] { 3, 60 })
        {
            var lines = Enumerable.Range(1, count).Select(n => new InvoiceLine($"منتج الجوهرة بلس رقم {n} - عبوة عناية مميزة", n % 3 + 1, 12.345m)).ToArray();
            var subtotal = lines.Sum(l => l.Price * l.Quantity);
            var data = new InvoiceData("INV-DEMO-2026", "ORDER-DEMO-001", "أحمد محمد", DateTime.UtcNow, lines, subtotal, 1m, 2m, 0m, subtotal + 1m);
            var pdf = await renderer.RenderAsync(data, Ct);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.AsSpan(0, 5)));
            Assert.True(pdf.Length > 10000);
            var output = Environment.GetEnvironmentVariable("ECOM_WHATSAPP_SAMPLE_DIR");
            if (output != null) { Directory.CreateDirectory(output); await File.WriteAllBytesAsync(Path.Combine(output, $"invoice-arabic-{count}.pdf"), pdf, Ct); }
        }
    }

    private WhatsAppInvoiceProcessor Processor(EcomDbContext db, IGupshupWhatsAppService sender, GupshupOptions? config = null) =>
        new(db, new FakeRenderer(), sender, Options.Create(config ?? Config()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppInvoiceProcessor>.Instance);

    private static WhatsAppInvoicePreparationRetry Recovery(EcomDbContext db, IInvoicePdfRenderer? renderer = null, bool allow = true)
    {
        var config = Config(); config.PreparationRetryInvoiceIds = allow ? [1] : [];
        return new(db, renderer ?? new FakeRenderer(), Options.Create(config),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppInvoicePreparationRetry>.Instance);
    }

    private async Task FailedPreparationAsync()
    {
        await SeedAsync();
        await using var db = Db();
        await db.WhatsAppInvoices.ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Failed)
            .SetProperty(j => j.LastErrorCode, "INVOICE_PREPARATION_FAILED").SetProperty(j => j.AttemptCount, 5), Ct);
    }

    [Fact]
    public async Task Preparation_recovery_is_audited_idempotent_and_does_not_charge_again()
    {
        await FailedPreparationAsync(); await using var db = Db();
        var request = Guid.NewGuid();
        var service = Recovery(db);
        Assert.True((await service.RetryAsync(1, 1, request, "admin-test", Ct)).Success);
        Assert.True((await service.RetryAsync(1, 1, request, "admin-test", Ct)).Success);
        Assert.False((await service.RetryAsync(1, 1, Guid.NewGuid(), "admin-test", Ct)).Success);
        var audit = await db.WhatsAppInvoiceRetryAudits.SingleAsync(Ct);
        Assert.Equal(5, audit.PreviousAttemptCount);
        Assert.Equal("INVOICE_PREPARATION_FAILED", audit.PreviousErrorCode);
        Assert.Equal("admin-test", audit.AdminId);
        var job = await db.WhatsAppInvoices.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(WhatsAppInvoiceStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.NotNull(job.Pdf);
        Assert.Null(job.DispatchStartedAtUtc);
        Assert.Equal(TransactionStatus.Paid, await db.Transactions.Select(t => t.Status).SingleAsync(Ct));
        Assert.Equal(PaymentAttemptStatus.Paid, await db.PaymentAttempts.Select(t => t.Status).SingleAsync(Ct));
        Assert.Equal(1, await db.PaymentAttempts.CountAsync(Ct));
        var sender = new FakeSender(new(WhatsAppSendOutcome.Accepted, "RECOVERED-ONCE"));
        await Processor(db, sender).ProcessBatchAsync("recovery-test", Ct);
        await Processor(db, sender).ProcessBatchAsync("recovery-test", Ct);
        Assert.Equal(1, sender.Calls);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("whatsapp")]
    [InlineData("dispatch")]
    [InlineData("event")]
    [InlineData("reconciliation")]
    [InlineData("token")]
    [InlineData("lease")]
    [InlineData("destination")]
    [InlineData("unknown")]
    [InlineData("delivery_failure")]
    [InlineData("unpaid")]
    [InlineData("refund")]
    [InlineData("no_consent")]
    [InlineData("wrong_consent")]
    [InlineData("revoked")]
    [InlineData("callback")]
    public async Task Preparation_recovery_rejects_any_dispatch_or_ineligible_order(string scenario)
    {
        await FailedPreparationAsync(); await using var db = Db();
        var job = await db.WhatsAppInvoices.Include(j => j.PaymentAttempt).ThenInclude(a => a.Order)
            .Include(j => j.PaymentAttempt).ThenInclude(a => a.Transaction).SingleAsync(Ct);
        switch (scenario)
        {
            case "provider": job.ProviderMessageId = "GS-EXISTS"; break;
            case "whatsapp": job.WhatsAppMessageId = "WA-EXISTS"; break;
            case "dispatch": job.DispatchStartedAtUtc = DateTime.UtcNow; break;
            case "event": job.LastEventAtUtc = DateTime.UtcNow; break;
            case "reconciliation": job.ReconciliationEvidence = "earlier uncertainty"; break;
            case "token": job.MediaTokenHash = new string('A', 64); break;
            case "lease": job.LeaseOwner = "active-worker"; break;
            case "destination": job.Destination = "+96550000000"; break;
            case "unknown": job.Status = WhatsAppInvoiceStatus.RequiresReconciliation; break;
            case "delivery_failure": job.LastErrorCode = "PROVIDER_DELIVERY_FAILED"; break;
            case "unpaid": job.PaymentAttempt.Status = PaymentAttemptStatus.AwaitingPayment; break;
            case "refund": job.PaymentAttempt.Transaction.IsRefunded = true; break;
            case "no_consent": job.PaymentAttempt.Order.WhatsAppConsentAtUtc = null; break;
            case "wrong_consent": job.PaymentAttempt.Order.WhatsAppConsentPhone = "+96560000000"; break;
            case "revoked": db.WhatsAppConsentAudits.Add(new() { Phone = "+96550000000", Action = WhatsAppConsentAction.Revoked, OccurredAtUtc = DateTime.UtcNow }); break;
            case "callback": db.WhatsAppDeliveryEvents.Add(new() { EventKey = new string('C', 64), AppName = Config().AppName,
                Destination = "+96550000000", ProviderMessageId = "UNMATCHED", Type = "sent", OccurredAtUtc = DateTime.UtcNow }); break;
        }
        await db.SaveChangesAsync(Ct); db.ChangeTracker.Clear();
        Assert.False((await Recovery(db).RetryAsync(1, 1, Guid.NewGuid(), "admin-test", Ct)).Success);
        Assert.Empty(await db.WhatsAppInvoiceRetryAudits.ToListAsync(Ct));
        Assert.Equal(5, await db.WhatsAppInvoices.Select(j => j.AttemptCount).SingleAsync(Ct));
    }

    [Fact]
    public async Task Recovery_requires_allowlist_order_identity_and_successful_pdf()
    {
        await FailedPreparationAsync(); await using var db = Db();
        Assert.False((await Recovery(db, allow: false).RetryAsync(1, 1, Guid.NewGuid(), "admin-test", Ct)).Success);
        Assert.False((await Recovery(db).RetryAsync(1, 48, Guid.NewGuid(), "admin-test", Ct)).Success);
        Assert.False((await Recovery(db).RetryAsync(1, 1, Guid.Empty, "admin-test", Ct)).Success);
        Assert.False((await Recovery(db).RetryAsync(1, 1, Guid.NewGuid(), "", Ct)).Success);
        Assert.Equal("PDF_VALIDATION_FAILED", (await Recovery(db, new BrokenRenderer()).RetryAsync(1, 1, Guid.NewGuid(), "admin-test", Ct)).Code);
        Assert.Equal(WhatsAppInvoiceStatus.Failed, await db.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
        Assert.Empty(await db.WhatsAppInvoiceRetryAudits.ToListAsync(Ct));
    }

    private sealed class BrokenRenderer : IInvoicePdfRenderer
    {
        public Task<byte[]> RenderAsync(InvoiceData invoice, CancellationToken cancellationToken) => throw new IOException("SECRET CUSTOMER");
    }

    [Fact]
    public async Task Concurrent_preparation_recoveries_commit_only_one_audit()
    {
        await FailedPreparationAsync();
        await using var one = Db(); await using var two = Db();
        var results = await Task.WhenAll(
            Recovery(one).RetryAsync(1, 1, Guid.NewGuid(), "admin-one", Ct),
            Recovery(two).RetryAsync(1, 1, Guid.NewGuid(), "admin-two", Ct));
        Assert.Single(results, r => r.Success);
        await using var check = Db();
        Assert.Equal(1, await check.WhatsAppInvoiceRetryAudits.CountAsync(Ct));
        Assert.Equal(WhatsAppInvoiceStatus.Pending, await check.WhatsAppInvoices.Select(j => j.Status).SingleAsync(Ct));
    }
    private static WhatsAppController Controller(EcomDbContext db, WhatsAppInvoiceProcessor processor) =>
        new(db, processor, Options.Create(Config())) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private static JsonElement Event(string id, string type) => JsonSerializer.SerializeToElement(new
    {
        app = Config().AppName, type = "message-event", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        payload = new { id = type is "enqueued" or "failed" ? id : "WA-1", gsId = id, type, destination = "96550000000" }
    });

    private async Task SeedAsync(PaymentAttemptStatus status = PaymentAttemptStatus.Paid, PaymentMethod method = PaymentMethod.Bank,
        bool consent = true, string phone = "50000000", string? consentPhone = null)
    {
        await using var db = Db();
        var product = new Product { Title = "منتج", Description = "منتج", newPrice = 12.345m,
            subCategory = new SubCategory { Name = "sub", Category = new Category { Name = "cat" } } };
        var order = new Order { OrderNumber = "ORDER-1", Status = OrderStatus.Processing, Subtotal = 12.345m, Total = 12.345m,
            ShippingAddress = new ShippingAddress { FullName = "أحمد", Phone = phone, Street = "شارع" },
            WhatsAppConsentAtUtc = consent ? DateTime.UtcNow : null, WhatsAppConsentPhone = consent ? consentPhone ?? phone : null,
            Items = [new OrderItem { Name = "منتج", Product = product, Quantity = 1, Price = 12.345m }] };
        var attempt = new PaymentAttempt { Order = order, Amount = 12.345m, Currency = "KWD", Status = status,
            IdempotencyKeyHash = new string('A', 64), RequestHash = new string('B', 64), MerchantReference = "MERCHANT-1",
            GatewayInvoiceId = "SADAD-1", GatewayVerifiedAtUtc = DateTime.UtcNow, VerificationEvidenceId = new string('E', 64),
            VerifiedAmount = 12.345m, VerifiedTotal = 12.345m, VerifiedRefundedAmount = 0m,
            Transaction = new Transaction { Order = order, Amount = 12.345m, Status = TransactionStatus.Paid, PaymentMethod = method } };
        db.WhatsAppInvoices.Add(new WhatsAppInvoice { PaymentAttempt = attempt, InvoiceNumber = "INV-1" });
        await db.SaveChangesAsync(Ct);
    }

    private sealed class FakeRenderer : IInvoicePdfRenderer
    {
        public Task<byte[]> RenderAsync(InvoiceData invoice, CancellationToken cancellationToken) => Task.FromResult("%PDF-test-fixture"u8.ToArray());
    }
    private sealed class FakeSender(WhatsAppSendResult result) : IGupshupWhatsAppService
    {
        private int _calls;
        public int Calls => _calls;
        public WhatsAppSendResult Result { get; set; } = result;
        public string? LastUrl { get; private set; }
        public Task<WhatsAppSendResult> SendInvoiceAsync(string destination, string orderNumber, string mediaUrl, CancellationToken cancellationToken)
        { Interlocked.Increment(ref _calls); LastUrl = mediaUrl; return Task.FromResult(Result); }
        public Task<WhatsAppSendResult> SendPhoneVerificationCodeAsync(string destination, string code, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
    private sealed class RecordingHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Url { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken); Url = request.RequestUri!.ToString();
            return new(status) { Content = new StringContent(response) };
        }
    }
}
