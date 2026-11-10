using System.Text.Json;
using AutoMapper;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Ecom.Application.Payments;

public sealed class PaymentOrchestrationService(
    EcomDbContext db,
    ISadadInvoiceGateway gateway,
    IEmailService email,
    IMapper mapper,
    IConfiguration configuration,
    ILogger<PaymentOrchestrationService> logger) : IPaymentOrchestrationService, IPaymentFinalizationService, ISadadPaymentVerificationService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StatusVerificationInterval = TimeSpan.FromSeconds(10);
    private sealed record PaymentPayload(int PaymentAttemptId);
    private sealed record OrderPayload(int OrderId);

    public async Task<int> ProcessOutboxBatchAsync(string workerId, int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ids = await db.OutboxMessages.AsNoTracking()
            .Where(m => (m.Status == OutboxStatus.Pending || (m.Status == OutboxStatus.Processing && m.LeaseUntilUtc < now)) && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.Id).Select(m => m.Id).Take(Math.Clamp(batchSize, 1, 100)).ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            var claimed = await db.OutboxMessages.Where(m => m.Id == id &&
                    (m.Status == OutboxStatus.Pending || (m.Status == OutboxStatus.Processing && m.LeaseUntilUtc < now)))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Processing)
                    .SetProperty(m => m.LeaseOwner, workerId).SetProperty(m => m.LeaseUntilUtc, now.Add(LeaseDuration))
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1), cancellationToken);
            if (claimed == 0) continue;
            db.ChangeTracker.Clear();
            var message = await db.OutboxMessages.SingleAsync(m => m.Id == id, cancellationToken);
            try
            {
                if (message.Type == "PaymentInvoiceRequested")
                    await ProcessPaymentDispatchAsync(message, cancellationToken);
                else if (message.Type is "OrderCreatedEmail" or "OrderStatusChangedEmail")
                    await ProcessEmailAsync(message, cancellationToken);
                else
                    throw new InvalidOperationException("UNKNOWN_OUTBOX_TYPE");
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox message processing failed for MessageId={MessageId}, Type={Type}.", message.Id, message.Type);
                db.ChangeTracker.Clear();
                await db.OutboxMessages.Where(m => m.Id == id && m.LeaseOwner == workerId)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, m => m.AttemptCount >= 8 ? OutboxStatus.DeadLetter : OutboxStatus.Pending)
                        .SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(Math.Min(300, 1 << Math.Min(message.AttemptCount, 8))))
                        .SetProperty(m => m.LeaseOwner, (string?)null).SetProperty(m => m.LeaseUntilUtc, (DateTime?)null)
                        .SetProperty(m => m.LastErrorCode, "PROCESSING_FAILED"), cancellationToken);
            }
        }
        return processed;
    }

    private async Task ProcessPaymentDispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PaymentPayload>(message.Payload) ?? throw new InvalidOperationException("INVALID_PAYLOAD");
        var attempt = await db.PaymentAttempts.SingleAsync(a => a.Id == payload.PaymentAttemptId, cancellationToken);
        if (attempt.Status is PaymentAttemptStatus.AwaitingPayment or PaymentAttemptStatus.Paid or PaymentAttemptStatus.Failed or PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.Expired or PaymentAttemptStatus.RequiresReconciliation)
        {
            await CompleteMessageAsync(message.Id, cancellationToken);
            return;
        }
        if (attempt.Status == PaymentAttemptStatus.Dispatching)
        {
            await TransitionAsync(attempt, PaymentAttemptStatus.RequiresReconciliation, "WORKER_RESTART_AFTER_DISPATCH", $"attempt:{attempt.Id}:dispatch-unknown", cancellationToken);
            await CompleteMessageAsync(message.Id, cancellationToken);
            return;
        }

        var claimed = await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status == PaymentAttemptStatus.PendingDispatch)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.Dispatching)
                .SetProperty(a => a.DispatchCount, a => a.DispatchCount + 1)
                .SetProperty(a => a.LastDispatchStartedAtUtc, DateTime.UtcNow), cancellationToken);
        if (claimed == 0) throw new DbUpdateConcurrencyException();
        db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
        {
            PaymentAttemptId = attempt.Id, FromStatus = PaymentAttemptStatus.PendingDispatch,
            ToStatus = PaymentAttemptStatus.Dispatching,
            EventKey = $"attempt:{attempt.Id}:dispatch:{attempt.DispatchCount + 1}:started", ReasonCode = "WORKER_CLAIMED"
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(a => a.Id == payload.PaymentAttemptId, cancellationToken);
        var result = await gateway.CreateInvoiceAsync(attempt, cancellationToken);

        if (result.Outcome == GatewayDispatchOutcome.RetryableBeforeSend)
        {
            await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status == PaymentAttemptStatus.Dispatching)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.PendingDispatch)
                    .SetProperty(a => a.LastErrorCode, result.ErrorCode).SetProperty(a => a.NextActionAtUtc, DateTime.UtcNow.AddSeconds(30)), cancellationToken);
            await db.OutboxMessages.Where(m => m.Id == message.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Pending).SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(30))
                    .SetProperty(m => m.LeaseOwner, (string?)null).SetProperty(m => m.LeaseUntilUtc, (DateTime?)null), cancellationToken);
            db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
            {
                PaymentAttemptId = attempt.Id, FromStatus = PaymentAttemptStatus.Dispatching,
                ToStatus = PaymentAttemptStatus.PendingDispatch,
                EventKey = $"attempt:{attempt.Id}:dispatch:{attempt.DispatchCount}:retry-before-send", ReasonCode = result.ErrorCode ?? "RETRYABLE_BEFORE_SEND"
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        if (result.Outcome == GatewayDispatchOutcome.Created)
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                var updated = await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status == PaymentAttemptStatus.Dispatching)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.AwaitingPayment)
                        .SetProperty(a => a.GatewayInvoiceId, result.InvoiceId).SetProperty(a => a.PaymentUrl, result.PaymentUrl)
                        .SetProperty(a => a.LastErrorCode, (string?)null), cancellationToken);
                if (updated != 1) throw new DbUpdateConcurrencyException();
                await db.Transactions.Where(t => t.Id == attempt.TransactionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.GatewayInvoiceId, result.InvoiceId)
                        .SetProperty(t => t.PaymentGatewayResponse, "InvoiceCreated"), cancellationToken);
                await CompleteMessageAsync(message.Id, cancellationToken);
                db.PaymentTransitionAudits.Add(new PaymentTransitionAudit { PaymentAttemptId = attempt.Id, FromStatus = PaymentAttemptStatus.Dispatching, ToStatus = PaymentAttemptStatus.AwaitingPayment, EventKey = $"attempt:{attempt.Id}:invoice-created", ReasonCode = "GATEWAY_CONFIRMED_CREATE" });
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            });
            return;
        }

        var target = result.Outcome == GatewayDispatchOutcome.Unknown ? PaymentAttemptStatus.RequiresReconciliation : PaymentAttemptStatus.Failed;
        await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status == PaymentAttemptStatus.Dispatching)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, target).SetProperty(a => a.LastErrorCode, result.ErrorCode), cancellationToken);
        db.PaymentTransitionAudits.Add(new PaymentTransitionAudit { PaymentAttemptId = attempt.Id, FromStatus = PaymentAttemptStatus.Dispatching, ToStatus = target, EventKey = $"attempt:{attempt.Id}:{target}", ReasonCode = result.ErrorCode ?? target.ToString() });
        await CompleteMessageAsync(message.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessEmailAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<OrderPayload>(message.Payload) ?? throw new InvalidOperationException("INVALID_PAYLOAD");
        var order = await db.Orders.Include(o => o.Items).Include(o => o.ShippingAddress).Include(o => o.Coupon).Include(o => o.AppUser)
            .AsNoTracking().SingleAsync(o => o.Id == payload.OrderId, cancellationToken);
        var destination = configuration["Notifications:AdminEmail"];
        if (string.IsNullOrWhiteSpace(destination)) throw new InvalidOperationException("ADMIN_EMAIL_NOT_CONFIGURED");
        var eventType = message.Type == "OrderCreatedEmail" ? "Created" : "StatusUpdated";
        if (!await email.SendOrderNotificationToAdminAsync(mapper.Map<OrderDto>(order), destination, eventType))
            throw new InvalidOperationException("EMAIL_DELIVERY_FAILED");
        await CompleteMessageAsync(message.Id, cancellationToken);
    }

    private Task CompleteMessageAsync(int id, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Processed)
            .SetProperty(m => m.ProcessedAtUtc, DateTime.UtcNow).SetProperty(m => m.LeaseOwner, (string?)null)
            .SetProperty(m => m.LeaseUntilUtc, (DateTime?)null), cancellationToken);

    private async Task TransitionAsync(PaymentAttempt attempt, PaymentAttemptStatus target, string reason, string eventKey, CancellationToken cancellationToken)
    {
        var from = attempt.Status;
        await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, target).SetProperty(a => a.LastErrorCode, reason), cancellationToken);
        db.PaymentTransitionAudits.Add(new PaymentTransitionAudit { PaymentAttemptId = attempt.Id, FromStatus = from, ToStatus = target, EventKey = eventKey, ReasonCode = reason });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ExpireReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var orderIds = await db.InventoryReservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now)
            .Select(r => r.OrderId).Distinct().Take(100).ToListAsync(cancellationToken);
        var expired = 0;
        foreach (var orderId in orderIds)
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                var reservations = await db.InventoryReservations.Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now).ToListAsync(cancellationToken);
                var releasedCount = 0;
                foreach (var reservation in reservations)
                {
                    var claimed = await db.InventoryReservations.Where(r => r.Id == reservation.Id && r.Status == ReservationStatus.Active)
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired).SetProperty(r => r.ReleasedAtUtc, now), cancellationToken);
                    if (claimed == 1)
                    {
                        await db.Products.Where(p => p.Id == reservation.ProductId).ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalInStock, p => p.TotalInStock + reservation.Quantity).SetProperty(p => p.IsInStock, true), cancellationToken);
                        releasedCount++;
                    }
                }
                if (releasedCount == 0)
                {
                    await tx.CommitAsync(cancellationToken);
                    return;
                }
                var coupon = await db.CouponReservations.SingleOrDefaultAsync(r => r.OrderId == orderId && r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now, cancellationToken);
                if (coupon != null)
                {
                    var claimed = await db.CouponReservations.Where(r => r.Id == coupon.Id && r.Status == ReservationStatus.Active)
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired).SetProperty(r => r.ReleasedAtUtc, now), cancellationToken);
                    if (claimed == 1) await db.Coupons.Where(c => c.Id == coupon.CouponId && c.UsedCount > 0).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount - 1), cancellationToken);
                }
                await db.Orders.Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Expired).SetProperty(o => o.GuestAccessRevokedAtUtc, now), cancellationToken);
                await db.PaymentAttempts.Where(a => a.OrderId == orderId && a.Status == PaymentAttemptStatus.AwaitingPayment)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.RequiresReconciliation).SetProperty(a => a.LastErrorCode, "RESERVATION_EXPIRED_AFTER_INVOICE"), cancellationToken);
                await db.PaymentAttempts.Where(a => a.OrderId == orderId && a.Status == PaymentAttemptStatus.PendingDispatch)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.Expired), cancellationToken);
                var expiredAttempts = await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == orderId &&
                    (a.Status == PaymentAttemptStatus.RequiresReconciliation || a.Status == PaymentAttemptStatus.Expired)).ToListAsync(cancellationToken);
                foreach (var paymentAttempt in expiredAttempts)
                    db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
                    {
                        PaymentAttemptId = paymentAttempt.Id,
                        FromStatus = paymentAttempt.Status == PaymentAttemptStatus.Expired ? PaymentAttemptStatus.PendingDispatch : PaymentAttemptStatus.AwaitingPayment,
                        ToStatus = paymentAttempt.Status,
                        EventKey = $"attempt:{paymentAttempt.Id}:reservation-expired", ReasonCode = "RESERVATION_EXPIRED"
                    });
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                expired++;
            });
        }
        return expired;
    }

    public Task<bool> FinalizeVerifiedAsync(int paymentAttemptId, string evidenceId, string gatewayInvoiceId, string merchantReference, string merchantIdentity, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Sadad:Verification:ContractProfileCertified") ||
            !configuration.GetValue<bool>("Sadad:Verification:EnablePaidTransitions"))
            return Task.FromResult(false);
        return FinalizeVerifiedCoreAsync(paymentAttemptId, new SadadVerifiedPayment(
            evidenceId, gatewayInvoiceId, 0, merchantReference, amount, amount, amount, 0m, currency, evidenceId),
            merchantIdentity, cancellationToken);
    }

    private async Task<bool> FinalizeVerifiedCoreAsync(
        int paymentAttemptId,
        SadadVerifiedPayment payment,
        string merchantIdentity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.EvidenceId)) throw new ArgumentException("Trusted evidence id is required.");
        db.ChangeTracker.Clear();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var attempt = await db.PaymentAttempts.Include(a => a.Order).SingleAsync(a => a.Id == paymentAttemptId, cancellationToken);
            var expectedMerchantIdentity = configuration["Sadad:MerchantVendorId"];
            if (string.IsNullOrWhiteSpace(expectedMerchantIdentity))
                expectedMerchantIdentity = configuration["Sadad:MerchantIdentity"];
            if (!string.Equals(attempt.GatewayInvoiceId, payment.GatewayInvoiceId, StringComparison.Ordinal) ||
                !string.Equals(attempt.MerchantReference, payment.MerchantReference, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(expectedMerchantIdentity) ||
                !string.Equals(expectedMerchantIdentity, merchantIdentity, StringComparison.Ordinal) ||
                !string.Equals(payment.Currency, attempt.Currency, StringComparison.Ordinal) ||
                !string.Equals(payment.Currency, "KWD", StringComparison.Ordinal) ||
                !KwdMoney.EqualsAtSettlementPrecision(payment.Amount, attempt.Amount) ||
                !KwdMoney.EqualsAtSettlementPrecision(payment.Total, attempt.Amount) ||
                !KwdMoney.EqualsAtSettlementPrecision(payment.RefundedAmount, 0m))
                return false;
            var verifiedAmount = KwdMoney.Round(payment.Amount);
            var verifiedTotal = KwdMoney.Round(payment.Total);
            var verifiedSubtotal = KwdMoney.Round(payment.Subtotal);
            var verifiedRefundedAmount = KwdMoney.Round(payment.RefundedAmount);
            if (attempt.Status == PaymentAttemptStatus.Paid) { await tx.CommitAsync(cancellationToken); return true; }
            var now = DateTime.UtcNow;
            var activeCount = await db.InventoryReservations.CountAsync(r => r.OrderId == attempt.OrderId && r.Status == ReservationStatus.Active && r.ExpiresAtUtc > now, cancellationToken);
            var totalCount = await db.InventoryReservations.CountAsync(r => r.OrderId == attempt.OrderId, cancellationToken);
            if (attempt.Order.Status != OrderStatus.Pending || activeCount == 0 || activeCount != totalCount)
            {
                await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.Status != PaymentAttemptStatus.Paid).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.RequiresReconciliation)
                    .SetProperty(a => a.LastErrorCode, "VERIFIED_AFTER_RESERVATION_RELEASE")
                    .SetProperty(a => a.GatewayVerifiedAtUtc, now)
                    .SetProperty(a => a.LastVerificationAtUtc, now)
                    .SetProperty(a => a.VerificationEvidenceId, payment.EvidenceId)
                    .SetProperty(a => a.GatewayTransactionId, payment.GatewayTransactionId)
                    .SetProperty(a => a.VerifiedAmount, verifiedAmount)
                    .SetProperty(a => a.VerifiedTotal, verifiedTotal)
                    .SetProperty(a => a.VerifiedSubtotal, verifiedSubtotal)
                    .SetProperty(a => a.VerifiedRefundedAmount, verifiedRefundedAmount)
                    .SetProperty(a => a.LeaseOwner, (string?)null).SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
                await db.Orders.Where(o => o.Id == attempt.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.RequiresReconciliation), cancellationToken);
                var reconciliationEventKey = $"verified:{payment.EvidenceId}:reconciliation";
                if (!await db.PaymentTransitionAudits.AnyAsync(a => a.EventKey == reconciliationEventKey, cancellationToken))
                    db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
                    {
                        PaymentAttemptId = attempt.Id,
                        FromStatus = attempt.Status,
                        ToStatus = PaymentAttemptStatus.RequiresReconciliation,
                        EventKey = reconciliationEventKey,
                        ReasonCode = "VERIFIED_AFTER_RESERVATION_RELEASE"
                    });
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return false;
            }
            var orderClaimed = await db.Orders.Where(o => o.Id == attempt.OrderId && o.Status == OrderStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Processing), cancellationToken);
            if (orderClaimed != 1)
            {
                await db.PaymentAttempts.Where(a => a.Id == attempt.Id &&
                        (a.Status == PaymentAttemptStatus.AwaitingPayment || a.Status == PaymentAttemptStatus.RequiresReconciliation))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.RequiresReconciliation)
                        .SetProperty(a => a.LastErrorCode, "VERIFIED_DURING_CONCURRENT_ORDER_TRANSITION")
                        .SetProperty(a => a.GatewayVerifiedAtUtc, now)
                        .SetProperty(a => a.LastVerificationAtUtc, now)
                        .SetProperty(a => a.VerificationEvidenceId, payment.EvidenceId)
                        .SetProperty(a => a.GatewayTransactionId, payment.GatewayTransactionId)
                        .SetProperty(a => a.VerifiedAmount, verifiedAmount)
                        .SetProperty(a => a.VerifiedTotal, verifiedTotal)
                        .SetProperty(a => a.VerifiedSubtotal, verifiedSubtotal)
                        .SetProperty(a => a.VerifiedRefundedAmount, verifiedRefundedAmount)
                        .SetProperty(a => a.LeaseOwner, (string?)null).SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
                await db.Orders.Where(o => o.Id == attempt.OrderId && o.Status != OrderStatus.RequiresReconciliation)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.RequiresReconciliation), cancellationToken);
                var reconciliationEventKey = $"verified:{payment.EvidenceId}:order-conflict";
                if (!await db.PaymentTransitionAudits.AnyAsync(a => a.EventKey == reconciliationEventKey, cancellationToken))
                    db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
                    {
                        PaymentAttemptId = attempt.Id,
                        FromStatus = attempt.Status,
                        ToStatus = PaymentAttemptStatus.RequiresReconciliation,
                        EventKey = reconciliationEventKey,
                        ReasonCode = "VERIFIED_DURING_CONCURRENT_ORDER_TRANSITION"
                    });
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return false;
            }
            var paymentClaimed = await db.PaymentAttempts.Where(a => a.Id == attempt.Id &&
                    (a.Status == PaymentAttemptStatus.AwaitingPayment || a.Status == PaymentAttemptStatus.RequiresReconciliation))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.Paid)
                    .SetProperty(a => a.GatewayVerifiedAtUtc, now)
                    .SetProperty(a => a.LastVerificationAtUtc, now)
                    .SetProperty(a => a.VerificationEvidenceId, payment.EvidenceId)
                    .SetProperty(a => a.GatewayTransactionId, payment.GatewayTransactionId)
                    .SetProperty(a => a.VerifiedAmount, verifiedAmount)
                    .SetProperty(a => a.VerifiedTotal, verifiedTotal)
                    .SetProperty(a => a.VerifiedSubtotal, verifiedSubtotal)
                    .SetProperty(a => a.VerifiedRefundedAmount, verifiedRefundedAmount)
                    .SetProperty(a => a.LastErrorCode, (string?)null)
                    .SetProperty(a => a.LeaseOwner, (string?)null)
                    .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
            if (paymentClaimed != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                return await db.PaymentAttempts.AsNoTracking().AnyAsync(a => a.Id == attempt.Id && a.Status == PaymentAttemptStatus.Paid, cancellationToken);
            }
            await db.InventoryReservations.Where(r => r.OrderId == attempt.OrderId && r.Status == ReservationStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Confirmed).SetProperty(r => r.ConfirmedAtUtc, now), cancellationToken);
            await db.CouponReservations.Where(r => r.OrderId == attempt.OrderId && r.Status == ReservationStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Confirmed).SetProperty(r => r.ConfirmedAtUtc, now), cancellationToken);
            await db.Transactions.Where(t => t.Id == attempt.TransactionId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TransactionStatus.Paid)
                    .SetProperty(t => t.GatewayVerifiedAtUtc, now)
                    .SetProperty(t => t.ProcessedDate, now)
                    .SetProperty(t => t.PaymentGatewayResponse, "Verified by Sadad GetById"), cancellationToken);
            db.PaymentTransitionAudits.Add(new PaymentTransitionAudit { PaymentAttemptId = attempt.Id, FromStatus = attempt.Status, ToStatus = PaymentAttemptStatus.Paid, EventKey = $"verified:{payment.EvidenceId}", ReasonCode = "STRICT_SADAD_GETBYID_VERIFICATION" });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<PaymentVerificationResult> VerifyByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == orderId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt == null || string.IsNullOrWhiteSpace(attempt.GatewayInvoiceId))
            return new(PaymentVerificationOutcome.NotFound, "INVOICE_NOT_READY");

        return await VerifyByInvoiceIdCoreAsync(attempt.GatewayInvoiceId, true, cancellationToken);
    }

    public Task<PaymentVerificationResult> VerifyByInvoiceIdAsync(
        string gatewayInvoiceId,
        string triggerId,
        CancellationToken cancellationToken = default) =>
        VerifyByInvoiceIdCoreAsync(gatewayInvoiceId, false, cancellationToken);

    private async Task<PaymentVerificationResult> VerifyByInvoiceIdCoreAsync(
        string gatewayInvoiceId,
        bool throttle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayInvoiceId))
            return new(PaymentVerificationOutcome.Rejected, "INVALID_INVOICE_ID");

        var attempt = await db.PaymentAttempts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.GatewayInvoiceId == gatewayInvoiceId, cancellationToken);
        if (attempt == null)
            return new(PaymentVerificationOutcome.NotFound, "UNKNOWN_INVOICE_ID");
        if (attempt.Status == PaymentAttemptStatus.Paid)
            return new(PaymentVerificationOutcome.AlreadyPaid, "ALREADY_VERIFIED_PAID");
        if (attempt.Status is PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.Failed or PaymentAttemptStatus.Expired)
            return new(PaymentVerificationOutcome.Rejected, "TERMINAL_PAYMENT_ATTEMPT");

        var worker = $"verify-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        var checkedBefore = now.Subtract(StatusVerificationInterval);
        var claimed = await db.PaymentAttempts.Where(a => a.Id == attempt.Id &&
                (a.Status == PaymentAttemptStatus.AwaitingPayment || a.Status == PaymentAttemptStatus.RequiresReconciliation) &&
                (a.LeaseUntilUtc == null || a.LeaseUntilUtc < now) &&
                (!throttle || a.LastVerificationAtUtc == null || a.LastVerificationAtUtc <= checkedBefore))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LeaseOwner, worker)
                .SetProperty(a => a.LeaseUntilUtc, now.Add(LeaseDuration)), cancellationToken);
        if (claimed == 0)
            return new(PaymentVerificationOutcome.Busy, "VERIFICATION_ALREADY_IN_PROGRESS");

        try
        {
            db.ChangeTracker.Clear();
            attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(a => a.Id == attempt.Id, cancellationToken);
            var result = await gateway.VerifyInvoiceAsync(attempt, cancellationToken);
            var checkedAt = DateTime.UtcNow;
            logger.LogInformation("Sadad invoice verification completed for PaymentAttemptId={PaymentAttemptId}. Status={Status}, ReasonCode={ReasonCode}",
                attempt.Id, result.Outcome.ToString(), result.ReasonCode);

            if (result.Outcome == SadadInvoiceVerificationOutcome.NotPaid)
            {
                await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.LeaseOwner == worker)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.AwaitingPayment)
                        .SetProperty(a => a.LastVerificationAtUtc, checkedAt)
                        .SetProperty(a => a.LastErrorCode, (string?)null)
                        .SetProperty(a => a.LeaseOwner, (string?)null)
                        .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
                return new(PaymentVerificationOutcome.NotPaid, result.ReasonCode);
            }

            // A verification precondition failure has no remote payment evidence.
            // Background checks must not hide an already-created payment link.
            if (result.Outcome == SadadInvoiceVerificationOutcome.Unsupported &&
                (result.ReasonCode is "MERCHANT_VENDOR_ID_NOT_CONFIGURED" or "DISCOUNT_SEMANTICS_NOT_CERTIFIED") &&
                attempt.Status == PaymentAttemptStatus.AwaitingPayment)
            {
                await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.LeaseOwner == worker)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastVerificationAtUtc, checkedAt)
                        .SetProperty(a => a.LastErrorCode, result.ReasonCode)
                        .SetProperty(a => a.LeaseOwner, (string?)null)
                        .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
                return new(PaymentVerificationOutcome.RequiresReconciliation, result.ReasonCode);
            }

            if (result.Outcome == SadadInvoiceVerificationOutcome.VerifiedPaid && result.Payment != null)
            {
                if (!configuration.GetValue<bool>("Sadad:Verification:ContractProfileCertified") ||
                    !configuration.GetValue<bool>("Sadad:Verification:EnablePaidTransitions"))
                {
                    await MarkRequiresReconciliationAsync(attempt.Id, worker, "PAID_TRANSITIONS_NOT_CERTIFIED", checkedAt, cancellationToken);
                    logger.LogWarning("Sadad payment confirmation blocked for PaymentAttemptId={PaymentAttemptId}. ReasonCode={ReasonCode}",
                        attempt.Id, "PAID_TRANSITIONS_NOT_CERTIFIED");
                    return new(PaymentVerificationOutcome.RequiresReconciliation, "PAID_TRANSITIONS_NOT_CERTIFIED");
                }

                var finalized = await FinalizeVerifiedCoreAsync(
                    attempt.Id,
                    result.Payment,
                    result.Payment.VendorId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken);
                if (!finalized)
                {
                    db.ChangeTracker.Clear();
                    var finalizationState = await db.PaymentAttempts.AsNoTracking()
                        .Where(a => a.Id == attempt.Id)
                        .Select(a => new { a.LeaseOwner, a.LastErrorCode })
                        .SingleAsync(cancellationToken);
                    var finalizationReason = finalizationState.LastErrorCode;
                    if (string.Equals(finalizationState.LeaseOwner, worker, StringComparison.Ordinal))
                    {
                        finalizationReason = "FINALIZATION_REJECTED";
                        await MarkRequiresReconciliationAsync(attempt.Id, worker, finalizationReason, checkedAt, cancellationToken);
                    }
                    finalizationReason ??= "FINALIZATION_REJECTED";
                    logger.LogWarning("Sadad payment finalization rejected for PaymentAttemptId={PaymentAttemptId}. ReasonCode={ReasonCode}",
                        attempt.Id, finalizationReason);
                    return new(PaymentVerificationOutcome.RequiresReconciliation, finalizationReason);
                }
                return new(PaymentVerificationOutcome.Paid, result.ReasonCode);
            }

            await MarkRequiresReconciliationAsync(attempt.Id, worker, result.ReasonCode, checkedAt, cancellationToken);
            return result.Outcome == SadadInvoiceVerificationOutcome.Invalid
                ? new(PaymentVerificationOutcome.Rejected, result.ReasonCode)
                : new(PaymentVerificationOutcome.RequiresReconciliation, result.ReasonCode);
        }
        catch
        {
            await db.PaymentAttempts.Where(a => a.Id == attempt.Id && a.LeaseOwner == worker)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LeaseOwner, (string?)null)
                    .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(a => a.LastVerificationAtUtc, DateTime.UtcNow)
                    .SetProperty(a => a.LastErrorCode, "VERIFICATION_UNAVAILABLE"), CancellationToken.None);
            throw;
        }
    }

    public async Task<int> VerifyPendingBatchAsync(
        string workerId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var attempts = await db.PaymentAttempts.AsNoTracking()
            .Where(a => a.Status == PaymentAttemptStatus.AwaitingPayment || a.Status == PaymentAttemptStatus.RequiresReconciliation)
            .OrderBy(a => a.LastVerificationAtUtc ?? a.CreatedAt)
            .Take(Math.Clamp(batchSize, 1, 100))
            .Select(a => new { a.Id, a.GatewayInvoiceId })
            .ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var item in attempts)
        {
            string? invoiceId = item.GatewayInvoiceId;
            if (string.IsNullOrWhiteSpace(invoiceId))
                invoiceId = await TryRecoverInvoiceIdAsync(item.Id, workerId, cancellationToken);
            if (string.IsNullOrWhiteSpace(invoiceId)) continue;

            await VerifyByInvoiceIdAsync(invoiceId, $"poll:{workerId}:{item.Id}", cancellationToken);
            processed++;
        }
        return processed;
    }

    private async Task<string?> TryRecoverInvoiceIdAsync(int attemptId, string workerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseOwner = $"reconcile-{workerId}-{Guid.NewGuid():N}";
        var claimed = await db.PaymentAttempts.Where(a => a.Id == attemptId &&
                a.Status == PaymentAttemptStatus.RequiresReconciliation && a.GatewayInvoiceId == null &&
                (a.LeaseUntilUtc == null || a.LeaseUntilUtc < now))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LeaseOwner, leaseOwner)
                .SetProperty(a => a.LeaseUntilUtc, now.Add(LeaseDuration)), cancellationToken);
        if (claimed == 0) return null;

        db.ChangeTracker.Clear();
        var attempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(a => a.Id == attemptId, cancellationToken);
        var lookup = await gateway.FindInvoiceByReferenceAsync(attempt, cancellationToken);
        if (lookup.Outcome != SadadInvoiceLookupOutcome.FoundUnique || string.IsNullOrWhiteSpace(lookup.InvoiceId))
        {
            await MarkRequiresReconciliationAsync(attemptId, leaseOwner, lookup.ReasonCode, DateTime.UtcNow, cancellationToken);
            return null;
        }

        try
        {
            var updated = await db.PaymentAttempts.Where(a => a.Id == attemptId && a.LeaseOwner == leaseOwner && a.GatewayInvoiceId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.GatewayInvoiceId, lookup.InvoiceId)
                    .SetProperty(a => a.Status, PaymentAttemptStatus.AwaitingPayment)
                    .SetProperty(a => a.LastErrorCode, (string?)null)
                    .SetProperty(a => a.LeaseOwner, (string?)null)
                    .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);
            if (updated != 1) return null;
            db.PaymentTransitionAudits.Add(new PaymentTransitionAudit
            {
                PaymentAttemptId = attemptId,
                FromStatus = PaymentAttemptStatus.RequiresReconciliation,
                ToStatus = PaymentAttemptStatus.AwaitingPayment,
                EventKey = $"attempt:{attemptId}:invoice-recovered:{HashEventPart(lookup.InvoiceId)}",
                ReasonCode = "GETALL_UNIQUE_CANDIDATE_REQUIRES_GETBYID"
            });
            await db.SaveChangesAsync(cancellationToken);
            return lookup.InvoiceId;
        }
        catch (DbUpdateException)
        {
            await MarkRequiresReconciliationAsync(attemptId, leaseOwner, "GETALL_INVOICE_ID_CONFLICT", DateTime.UtcNow, cancellationToken);
            return null;
        }
    }

    private Task<int> MarkRequiresReconciliationAsync(
        int attemptId,
        string leaseOwner,
        string reasonCode,
        DateTime checkedAt,
        CancellationToken cancellationToken) =>
        db.PaymentAttempts.Where(a => a.Id == attemptId && a.LeaseOwner == leaseOwner && a.Status != PaymentAttemptStatus.Paid)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.RequiresReconciliation)
                .SetProperty(a => a.LastVerificationAtUtc, checkedAt)
                .SetProperty(a => a.LastErrorCode, reasonCode.Length <= 100 ? reasonCode : reasonCode[..100])
                .SetProperty(a => a.LeaseOwner, (string?)null)
                .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null), cancellationToken);

    private static string HashEventPart(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
