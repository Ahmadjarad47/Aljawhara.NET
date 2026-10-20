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
    [InlineData("50000000", "96550000000")]
    [InlineData("+965 5000-0000", "96550000000")]
    [InlineData("0096550000000", "96550000000")]
    [InlineData("96550000000", "96550000000")]
    [InlineData("+4950000000", null)]
    [InlineData("050000000", null)]
    [InlineData("5000000", null)]
    [InlineData("50000000x", null)]
    [InlineData("965+50000000", null)]
    public void Country_prefix_normalization_and_validation(string input, string? expected) =>
        Assert.Equal(expected, WhatsAppPhone.Normalize(input, Config()));

    [Fact]
    public void Country_can_be_changed_in_settings()
    {
        var o = Config(); o.CountryCode = "49"; o.NationalNumberPattern = "1[5-7][0-9]{8,9}";
        Assert.Equal("4915207610021", WhatsAppPhone.Normalize("15207610021", o));
        Assert.Equal("4915207610021", WhatsAppPhone.Normalize("+4915207610021", o));
        Assert.Null(WhatsAppPhone.Normalize("+96550000000", o));
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
                .SendInvoiceAsync("96550000000", "ORDER-1", "https://api.example.test/invoice?token=test", Ct);
            Assert.Equal(WhatsAppSendOutcome.Accepted, result.Outcome);
            Assert.Equal("GS-123", result.MessageId);
            Assert.Contains(production ? "/template/msg" : "/v1/msg", handler.Url);
            var values = handler.Body!.Split('&').Select(s => s.Split('=', 2))
                .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
            using var json = JsonDocument.Parse(values["message"]);
            Assert.Equal(production ? "document" : "file", json.RootElement.GetProperty("type").GetString());
            var file = production ? json.RootElement.GetProperty("document") : json.RootElement;
            Assert.EndsWith(".pdf", file.GetProperty("filename").GetString());
            Assert.Equal("https://api.example.test/invoice?token=test", file.GetProperty(production ? "link" : "url").GetString());
            Assert.Equal(production, values.ContainsKey("template"));
        }
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
            .SendInvoiceAsync("96550000000", "ORDER-1", "https://api.example.test/file", Ct)).Outcome);
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
        Assert.Equal("96550000000", invoice.Destination);
        Assert.NotNull(invoice.Pdf);
        Assert.NotNull(invoice.MediaTokenHash);
        Assert.DoesNotContain(invoice.MediaTokenHash, sender.LastUrl!);
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
        new(db, new FakeRenderer(), sender, Options.Create(config ?? Config()));
    private static WhatsAppController Controller(EcomDbContext db, WhatsAppInvoiceProcessor processor) =>
        new(db, processor, Options.Create(Config())) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private static JsonElement Event(string id, string type) => JsonSerializer.SerializeToElement(new
    {
        app = Config().AppName, type = "message-event", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        payload = new { id = type is "enqueued" or "failed" ? id : "WA-1", gsId = id, type, destination = "96550000000" }
    });

    private async Task SeedAsync(PaymentAttemptStatus status = PaymentAttemptStatus.Paid, PaymentMethod method = PaymentMethod.Bank,
        bool consent = true, string phone = "50000000")
    {
        await using var db = Db();
        var product = new Product { Title = "منتج", Description = "منتج", newPrice = 12.345m,
            subCategory = new SubCategory { Name = "sub", Category = new Category { Name = "cat" } } };
        var order = new Order { OrderNumber = "ORDER-1", Status = OrderStatus.Processing, Subtotal = 12.345m, Total = 12.345m,
            ShippingAddress = new ShippingAddress { FullName = "أحمد", Phone = phone, Street = "شارع" },
            WhatsAppConsentAtUtc = consent ? DateTime.UtcNow : null, WhatsAppConsentPhone = consent ? phone : null,
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
