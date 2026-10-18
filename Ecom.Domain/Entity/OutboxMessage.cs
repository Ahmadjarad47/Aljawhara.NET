using Ecom.Domain.comman;
using Ecom.Domain.constant;

namespace Ecom.Domain.Entity;

public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public uint RowVersion { get; set; }
}
