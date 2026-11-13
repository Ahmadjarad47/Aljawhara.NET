using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecom.Application.Telegram;

public sealed class TelegramOrderNotificationProcessor(
    EcomDbContext db,
    TelegramOrderNotificationService sender,
    IOptions<TelegramOptions> options,
    ILogger<TelegramOrderNotificationProcessor> logger)
{
    public const string OutboxType = "PaidOrderTelegram";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(3);

    public async Task<int> ProcessBatchAsync(string workerId, int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return 0;

        var now = DateTime.UtcNow;
        var ids = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == OutboxType &&
                        (m.Status == OutboxStatus.Pending ||
                         (m.Status == OutboxStatus.Processing && m.LeaseUntilUtc < now)) &&
                        m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .Take(Math.Clamp(batchSize, 1, 100))
            .ToListAsync(cancellationToken);
        var processed = 0;

        foreach (var id in ids)
        {
            var claimed = await db.OutboxMessages
                .Where(m => m.Id == id && m.Type == OutboxType &&
                            (m.Status == OutboxStatus.Pending ||
                             (m.Status == OutboxStatus.Processing && m.LeaseUntilUtc < now)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, OutboxStatus.Processing)
                    .SetProperty(m => m.LeaseOwner, workerId)
                    .SetProperty(m => m.LeaseUntilUtc, now.Add(LeaseDuration))
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1), cancellationToken);
            if (claimed == 0) continue;

            db.ChangeTracker.Clear();
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, cancellationToken);
            try
            {
                if (!int.TryParse(message.Payload, out var orderId) || orderId <= 0)
                    throw new TelegramDeliveryException(0, false, null);
                await sender.SendPaidOrderAsync(orderId, cancellationToken);
                await db.OutboxMessages.Where(m => m.Id == id && m.LeaseOwner == workerId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, OutboxStatus.Processed)
                        .SetProperty(m => m.ProcessedAtUtc, DateTime.UtcNow)
                        .SetProperty(m => m.LeaseOwner, (string?)null)
                        .SetProperty(m => m.LeaseUntilUtc, (DateTime?)null)
                        .SetProperty(m => m.LastErrorCode, (string?)null), cancellationToken);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delivery = ex as TelegramDeliveryException;
                var retryable = delivery?.Retryable ?? true;
                var exhausted = message.AttemptCount >= options.Value.MaxAttempts;
                var delay = delivery?.RetryAfter ??
                            TimeSpan.FromSeconds(Math.Min(300, 1 << Math.Min(message.AttemptCount, 8)));
                var errorCode = delivery?.StatusCode > 0
                    ? $"TELEGRAM_HTTP_{delivery.StatusCode}"
                    : $"TELEGRAM_{ex.GetType().Name.ToUpperInvariant()}";
                if (errorCode.Length > 100) errorCode = errorCode[..100];
                logger.LogWarning(
                    "Telegram notification failed for OutboxMessageId={MessageId}; ReasonCode={ReasonCode}; Retryable={Retryable}; Attempt={Attempt}.",
                    id, errorCode, retryable, message.AttemptCount);
                await db.OutboxMessages.Where(m => m.Id == id && m.LeaseOwner == workerId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, !retryable || exhausted ? OutboxStatus.DeadLetter : OutboxStatus.Pending)
                        .SetProperty(m => m.NextAttemptAtUtc, DateTime.UtcNow.Add(delay))
                        .SetProperty(m => m.LeaseOwner, (string?)null)
                        .SetProperty(m => m.LeaseUntilUtc, (DateTime?)null)
                        .SetProperty(m => m.LastErrorCode, errorCode), cancellationToken);
            }
        }

        return processed;
    }
}
