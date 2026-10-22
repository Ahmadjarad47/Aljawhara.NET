using System.Data;
using System.Security.Cryptography;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecom.Application.WhatsApp;

public sealed record PreparationRetryResult(bool Success, string Code);

public sealed class WhatsAppInvoicePreparationRetry(EcomDbContext db, IInvoicePdfRenderer renderer,
    IOptions<GupshupOptions> options, ILogger<WhatsAppInvoicePreparationRetry> logger)
{
    public async Task<PreparationRetryResult> RetryAsync(int id, int expectedOrderId, Guid requestId, string adminId,
        CancellationToken ct)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(adminId) || adminId.Length > 450)
            return new(false, "INVALID_ADMIN_OR_REQUEST_ID");
        if (!options.Value.PreparationRetryInvoiceIds.Contains(id)) return new(false, "INVOICE_NOT_ALLOWLISTED");

        // The strategy also handles commit-acknowledgement loss: replay finds the durable audit.
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var existing = await db.WhatsAppInvoiceRetryAudits.AsNoTracking()
                    .SingleOrDefaultAsync(a => a.RequestId == requestId, ct);
                if (existing != null)
                    return new PreparationRetryResult(existing.InvoiceId == id && existing.OrderId == expectedOrderId && existing.AdminId == adminId,
                        "REQUEST_ALREADY_RECORDED");
                if (await db.WhatsAppInvoiceRetryAudits.AnyAsync(a => a.InvoiceId == id, ct))
                    return new PreparationRetryResult(false, "INVOICE_ALREADY_RETRIED");
    
                var job = await SafeJobs(id).Include(j => j.PaymentAttempt).ThenInclude(a => a.Transaction)
                    .Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(o => o.Items)
                    .Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(o => o.ShippingAddress)
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (job == null || job.PaymentAttempt.OrderId != expectedOrderId)
                    return new PreparationRetryResult(false, "NOT_A_NEVER_DISPATCHED_PREPARATION_FAILURE");
                var a = job.PaymentAttempt;
                var order = a.Order;
                var payment = a.Transaction;
                if (a.Status != PaymentAttemptStatus.Paid || payment.Status != TransactionStatus.Paid ||
                    payment.PaymentMethod == PaymentMethod.Cod || payment.IsRefunded ||
                    order.Status is not (OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered) ||
                    a.GatewayVerifiedAtUtc == null || string.IsNullOrWhiteSpace(a.VerificationEvidenceId) ||
                    string.IsNullOrWhiteSpace(a.GatewayInvoiceId) || a.Currency != "KWD" ||
                    a.VerifiedAmount != a.Amount || a.VerifiedTotal != order.Total || a.VerifiedRefundedAmount != 0m)
                    return new PreparationRetryResult(false, "PAYMENT_NOT_ELIGIBLE");
                var destination = WhatsAppPhone.Normalize(order.WhatsAppConsentPhone, options.Value);
                if (order.WhatsAppConsentAtUtc == null || destination == null ||
                    destination != WhatsAppPhone.Normalize(order.ShippingAddress.Phone, options.Value))
                    return new PreparationRetryResult(false, "NO_MATCHING_CUSTOMER_CONSENT");
                if (await db.WhatsAppConsentAudits.AnyAsync(c => c.Phone == destination &&
                        c.Action == WhatsAppConsentAction.Revoked && c.OccurredAtUtc >= order.WhatsAppConsentAtUtc, ct))
                    return new PreparationRetryResult(false, "CUSTOMER_CONSENT_REVOKED");
                if (options.Value.Environment == "Sandbox" && !WhatsAppPhone.IsSandboxAllowed(destination, options.Value))
                    return new PreparationRetryResult(false, "SANDBOX_DESTINATION_NOT_ALLOWED");
                // Fail closed on unmatched callbacks as well as all persisted dispatch markers.
                if (await db.WhatsAppDeliveryEvents.AnyAsync(e => e.AppName == options.Value.AppName &&
                        e.Destination == destination && e.OccurredAtUtc >= job.CreatedAt, ct))
                    return new PreparationRetryResult(false, "POSSIBLE_PREVIOUS_PROVIDER_ACCEPTANCE");
    
                byte[] pdf;
                try
                {
                    pdf = await renderer.RenderAsync(new InvoiceData(job.InvoiceNumber, order.OrderNumber,
                        order.ShippingAddress.FullName, a.GatewayVerifiedAtUtc.Value,
                        order.Items.OrderBy(i => i.Id).Select(i => new InvoiceLine(i.Name, i.Quantity, i.Price)).ToArray(),
                        order.Subtotal, order.CouponDiscountAmount ?? 0m, order.Shipping, order.Tax, order.Total), ct);
                    if (pdf.Length is < 5 or > 10 * 1024 * 1024 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                        throw new InvalidOperationException("INVALID_INVOICE_PDF");
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    InvoiceFailureLog.Write(logger, id, ex);
                    return new PreparationRetryResult(false, "PDF_VALIDATION_FAILED");
                }
    
                var now = DateTime.UtcNow;
                var changed = await SafeJobs(id).Where(j => j.RowVersion == job.RowVersion &&
                        j.PaymentAttempt.OrderId == expectedOrderId)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, WhatsAppInvoiceStatus.Pending)
                        .SetProperty(j => j.AttemptCount, 0).SetProperty(j => j.Pdf, pdf)
                        .SetProperty(j => j.LastErrorCode, (string?)null).SetProperty(j => j.NextAttemptAtUtc, now)
                        .SetProperty(j => j.UpdatedAt, now).SetProperty(j => j.UpdatedBy, adminId), ct);
                if (changed != 1) return new PreparationRetryResult(false, "CONCURRENT_INVOICE_CHANGE");
                // Use a separate audit entity; reconciliation must never overwrite recovery evidence.
                // Raw SQL avoids retaining a tracked Added audit if the execution strategy retries.
                var hash = Convert.ToHexString(SHA256.HashData(pdf));
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "WhatsAppInvoiceRetryAudits"
                    ("InvoiceId", "OrderId", "RequestId", "AdminId", "PreviousAttemptCount", "PreviousErrorCode",
                     "PdfSha256", "PdfValidatedAtUtc", "CreatedAt", "CreatedBy", "IsDeleted", "IsActive")
                    VALUES ({id}, {expectedOrderId}, {requestId}, {adminId}, {job.AttemptCount}, {job.LastErrorCode},
                            {hash}, {now}, {now}, {adminId}, FALSE, TRUE)
                    """, ct);
                await tx.CommitAsync(ct);
                return new PreparationRetryResult(true, "PREPARATION_RETRY_QUEUED");
            });
        }
        catch (Exception ex) when (ex.GetBaseException() is PostgresException
            { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation })
        {
            var audit = await db.WhatsAppInvoiceRetryAudits.AsNoTracking().SingleOrDefaultAsync(a => a.RequestId == requestId, ct);
            return new(audit != null && audit.InvoiceId == id && audit.OrderId == expectedOrderId && audit.AdminId == adminId,
                "CONCURRENT_RECOVERY_REQUEST");
        }
    }

    private IQueryable<WhatsAppInvoice> SafeJobs(int id) => db.WhatsAppInvoices.Where(j => j.Id == id &&
        !j.IsDeleted && j.IsActive && j.Status == WhatsAppInvoiceStatus.Failed &&
        j.LastErrorCode == "INVOICE_PREPARATION_FAILED" && j.ProviderMessageId == null && j.WhatsAppMessageId == null &&
        j.DispatchStartedAtUtc == null && j.LastEventAtUtc == null && j.ReconciliationEvidence == null &&
        j.MediaTokenHash == null && j.MediaExpiresAtUtc == null && j.Destination == null && j.AppName == null &&
        j.LeaseOwner == null && j.LeaseUntilUtc == null);
}
