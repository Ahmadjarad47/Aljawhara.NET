using System.Security.Cryptography;
using System.Text;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Ecom.Application.WhatsApp;

public sealed class WhatsAppInvoiceProcessor(EcomDbContext db, IInvoicePdfRenderer renderer,
    IGupshupWhatsAppService sender, IOptions<GupshupOptions> options, ILogger<WhatsAppInvoiceProcessor> logger)
{
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<int> ProcessBatchAsync(string worker, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled) return 0;
        var now = DateTime.UtcNow;
        // A process may have died after the provider accepted the POST. Never blindly reclaim it.
        await db.WhatsAppInvoices.Where(j => j.Status == WhatsAppInvoiceStatus.Dispatching && j.LeaseUntilUtc < now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.RequiresReconciliation)
                .SetProperty(j => j.LastErrorCode, "DISPATCH_LEASE_EXPIRED_UNKNOWN_OUTCOME"), ct);
        var cutoff = now.AddDays(-o.PdfRetentionDays);
        await db.WhatsAppInvoices.Where(j => j.CreatedAt < cutoff && j.Pdf != null &&
                (j.MediaExpiresAtUtc == null || j.MediaExpiresAtUtc < now) &&
                j.Status != WhatsAppInvoiceStatus.Preparing && j.Status != WhatsAppInvoiceStatus.Dispatching)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Pdf, (byte[]?)null).SetProperty(j => j.MediaTokenHash, (string?)null), ct);
        await db.WhatsAppDeliveryEvents.Where(e => e.CreatedAt < now.AddDays(-30)).ExecuteDeleteAsync(ct);
        var ids = await db.WhatsAppInvoices.AsNoTracking().Where(j => j.NextAttemptAtUtc <= now &&
                (j.Status == WhatsAppInvoiceStatus.Pending || (j.Status == WhatsAppInvoiceStatus.Preparing && j.LeaseUntilUtc < now)))
            .OrderBy(j => j.Id).Select(j => j.Id).Take(10).ToListAsync(ct);
        foreach (var id in ids)
        {
            now = DateTime.UtcNow;
            var claimed = await db.WhatsAppInvoices.Where(j => j.Id == id && j.NextAttemptAtUtc <= now &&
                    (j.Status == WhatsAppInvoiceStatus.Pending || (j.Status == WhatsAppInvoiceStatus.Preparing && j.LeaseUntilUtc < now)))
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Preparing)
                    .SetProperty(j => j.LeaseOwner, worker).SetProperty(j => j.LeaseUntilUtc, now.AddMinutes(2))
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1), ct);
            if (claimed == 0) continue;
            try { await ProcessAsync(id, worker, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                InvoiceFailureLog.Write(logger, id, ex);
                // Preparation failures are safe to retry. Dispatch failures remain ambiguous.
                await RetryPreparationAsync(id, worker, "INVOICE_PREPARATION_FAILED", ct);
            }
        }
        return ids.Count;
    }

    private async Task ProcessAsync(int id, string worker, CancellationToken ct)
    {
        var o = options.Value;
        var job = await db.WhatsAppInvoices.AsNoTracking().Include(j => j.PaymentAttempt).ThenInclude(a => a.Transaction)
            .Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(order => order.Items)
            .Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(order => order.ShippingAddress)
            .SingleAsync(j => j.Id == id, ct);
        var attempt = job.PaymentAttempt;
        var order = attempt.Order;
        var transaction = attempt.Transaction;
        if (attempt.Status != PaymentAttemptStatus.Paid || transaction.Status != TransactionStatus.Paid ||
            transaction.PaymentMethod == PaymentMethod.Cod || transaction.IsRefunded ||
            order.Status is not (OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered) ||
            attempt.GatewayVerifiedAtUtc == null || string.IsNullOrWhiteSpace(attempt.VerificationEvidenceId) ||
            string.IsNullOrWhiteSpace(attempt.GatewayInvoiceId) || attempt.Currency != "KWD" ||
            attempt.VerifiedAmount != attempt.Amount || attempt.VerifiedTotal != order.Total || attempt.VerifiedRefundedAmount != 0m)
        { await SkipAsync(id, worker, "PAYMENT_NOT_ELIGIBLE", ct); return; }
        if (order.WhatsAppConsentAtUtc == null || string.IsNullOrWhiteSpace(order.WhatsAppConsentPhone))
        { await SkipAsync(id, worker, "NO_MATCHING_CUSTOMER_CONSENT", ct); return; }
        var destination = WhatsAppPhone.Normalize(order.WhatsAppConsentPhone, o);
        var recipient = WhatsAppPhone.Normalize(order.ShippingAddress.Phone, o);
        if (destination == null || recipient == null)
        { await SkipAsync(id, worker, "INVALID_CUSTOMER_PHONE", ct); return; }
        if (!string.Equals(destination, recipient, StringComparison.Ordinal))
        { await SkipAsync(id, worker, "NO_MATCHING_CUSTOMER_CONSENT", ct); return; }
        var latestConsent = await db.WhatsAppConsentAudits.AsNoTracking()
            .Where(a => a.Phone == destination)
            .OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)
            .Select(a => new { a.Action, a.OccurredAtUtc }).FirstOrDefaultAsync(ct);
        if (latestConsent?.Action == WhatsAppConsentAction.Revoked &&
            latestConsent.OccurredAtUtc >= order.WhatsAppConsentAtUtc.Value)
        { await SkipAsync(id, worker, "CUSTOMER_CONSENT_REVOKED", ct); return; }
        if (o.Environment == "Sandbox" && !WhatsAppPhone.IsSandboxAllowed(destination, o))
        { await SkipAsync(id, worker, "SANDBOX_DESTINATION_NOT_ALLOWED", ct); return; }
        if (job.AttemptCount > o.MaxAttempts) { await SkipAsync(id, worker, "RETRY_LIMIT_EXCEEDED", ct); return; }
        var pdf = job.Pdf ?? await renderer.RenderAsync(new InvoiceData(job.InvoiceNumber, order.OrderNumber,
            order.ShippingAddress.FullName, attempt.GatewayVerifiedAtUtc.Value,
            order.Items.OrderBy(i => i.Id).Select(i => new InvoiceLine(i.Name, i.Quantity, i.Price)).ToArray(),
            order.Subtotal, order.CouponDiscountAmount ?? 0m, order.Shipping, order.Tax, order.Total), ct);
        if (pdf.Length is < 5 or > 10 * 1024 * 1024 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidOperationException("INVALID_INVOICE_PDF");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        // Persist the uncertainty barrier BEFORE any network I/O, fenced against stale workers.
        var ready = await db.WhatsAppInvoices.Where(j => j.Id == id && j.Status == WhatsAppInvoiceStatus.Preparing &&
                j.LeaseOwner == worker && j.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Dispatching)
                .SetProperty(j => j.Pdf, pdf).SetProperty(j => j.MediaTokenHash, tokenHash)
                .SetProperty(j => j.MediaExpiresAtUtc, now.AddMinutes(o.MediaLifetimeMinutes))
                .SetProperty(j => j.Destination, destination).SetProperty(j => j.AppName, o.AppName)
                .SetProperty(j => j.DispatchStartedAtUtc, now).SetProperty(j => j.LeaseUntilUtc, now.AddMinutes(2)), ct);
        if (ready == 0) return;
        var mediaUrl = $"{o.PublicBaseUrl.TrimEnd('/')}/api/whatsapp/invoices/{id}/document?token={token}";
        var result = await sender.SendInvoiceAsync(destination, order.OrderNumber, mediaUrl, ct);
        var state = result.Outcome switch
        {
            WhatsAppSendOutcome.Accepted => WhatsAppInvoiceStatus.Accepted,
            WhatsAppSendOutcome.RetryableRejection when job.AttemptCount < o.MaxAttempts => WhatsAppInvoiceStatus.Pending,
            WhatsAppSendOutcome.Rejected or WhatsAppSendOutcome.RetryableRejection => WhatsAppInvoiceStatus.Failed,
            _ => WhatsAppInvoiceStatus.RequiresReconciliation
        };
        await db.WhatsAppInvoices.Where(j => j.Id == id && j.LeaseOwner == worker &&
                (j.Status == WhatsAppInvoiceStatus.Dispatching || j.Status == WhatsAppInvoiceStatus.RequiresReconciliation))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, state).SetProperty(j => j.ProviderMessageId, result.MessageId)
                .SetProperty(j => j.LastErrorCode, result.ErrorCode)
                .SetProperty(j => j.NextAttemptAtUtc, now.AddSeconds(BackoffSeconds(job.AttemptCount)))
                .SetProperty(j => j.LeaseOwner, (string?)null).SetProperty(j => j.LeaseUntilUtc, (DateTime?)null), ct);
        if (result.MessageId != null) await ApplyEventsAsync(result.MessageId, ct);
    }

    public static int BackoffSeconds(int attemptCount) => Math.Min(3600, 15 * (1 << Math.Clamp(attemptCount, 1, 8)));

    private Task<int> SkipAsync(int id, string worker, string code, CancellationToken ct) =>
        db.WhatsAppInvoices.Where(j => j.Id == id && j.LeaseOwner == worker && j.Status == WhatsAppInvoiceStatus.Preparing)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Skipped)
                .SetProperty(j => j.LastErrorCode, code).SetProperty(j => j.LeaseOwner, (string?)null)
                .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null), ct);

    private async Task RetryPreparationAsync(int id, string worker, string code, CancellationToken ct)
    {
        var job = await db.WhatsAppInvoices.AsNoTracking().SingleAsync(j => j.Id == id, ct);
        await db.WhatsAppInvoices.Where(j => j.Id == id && j.LeaseOwner == worker && j.Status == WhatsAppInvoiceStatus.Preparing)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status,
                    job.AttemptCount >= options.Value.MaxAttempts ? WhatsAppInvoiceStatus.Failed : WhatsAppInvoiceStatus.Pending)
                .SetProperty(j => j.LastErrorCode, code).SetProperty(j => j.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(BackoffSeconds(job.AttemptCount)))
                .SetProperty(j => j.LeaseOwner, (string?)null).SetProperty(j => j.LeaseUntilUtc, (DateTime?)null), ct);
    }

    public async Task ApplyEventsAsync(string messageId, CancellationToken ct)
    {
        var events = await db.WhatsAppDeliveryEvents.AsNoTracking().Where(e => e.ProviderMessageId == messageId)
            .OrderBy(e => e.OccurredAtUtc).ToListAsync(ct);
        foreach (var e in events)
        {
            var status = e.Type switch
            {
                "enqueued" => WhatsAppInvoiceStatus.Accepted, "sent" => WhatsAppInvoiceStatus.Sent,
                "delivered" => WhatsAppInvoiceStatus.Delivered, "read" => WhatsAppInvoiceStatus.Read,
                "failed" => WhatsAppInvoiceStatus.Failed, _ => (WhatsAppInvoiceStatus?)null
            };
            if (status == null) continue;
            var q = db.WhatsAppInvoices.Where(j => j.ProviderMessageId == messageId && j.AppName == e.AppName && j.Destination == e.Destination);
            // Monotonic delivery state even when Gupshup delivers callbacks out of order.
            q = e.Type switch
            {
                "read" => q,
                "delivered" => q.Where(j => j.Status != WhatsAppInvoiceStatus.Read),
                "sent" => q.Where(j => j.Status != WhatsAppInvoiceStatus.Read && j.Status != WhatsAppInvoiceStatus.Delivered),
                "failed" => q.Where(j => j.Status != WhatsAppInvoiceStatus.Read && j.Status != WhatsAppInvoiceStatus.Delivered &&
                    (j.LastEventAtUtc == null || j.LastEventAtUtc <= e.OccurredAtUtc)),
                _ => q.Where(j => j.Status == WhatsAppInvoiceStatus.Accepted || j.Status == WhatsAppInvoiceStatus.RequiresReconciliation)
            };
            await q.ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status.Value)
                .SetProperty(j => j.LastEventAtUtc, j => j.LastEventAtUtc == null || j.LastEventAtUtc < e.OccurredAtUtc ? e.OccurredAtUtc : j.LastEventAtUtc)
                .SetProperty(j => j.WhatsAppMessageId, j => e.WhatsAppMessageId ?? j.WhatsAppMessageId)
                .SetProperty(j => j.LastErrorCode, e.Type == "failed" ? "PROVIDER_DELIVERY_FAILED" : null), ct);
        }
    }
}
