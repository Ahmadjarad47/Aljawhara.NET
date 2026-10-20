using Ecom.Domain.comman;

namespace Ecom.Domain.Entity;

public enum WhatsAppInvoiceStatus
{
    Pending, Preparing, Dispatching, Accepted, Sent, Delivered, Read,
    Failed, Skipped, RequiresReconciliation
}

// A specialized durable outbox: one logical invoice notification per verified attempt.
public sealed class WhatsAppInvoice : BaseEntity
{
    public int PaymentAttemptId { get; set; }
    public PaymentAttempt PaymentAttempt { get; set; } = null!;
    public WhatsAppInvoiceStatus Status { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string? Destination { get; set; }
    public string? AppName { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? WhatsAppMessageId { get; set; }
    public byte[]? Pdf { get; set; }
    public string? MediaTokenHash { get; set; }
    public DateTime? MediaExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? DispatchStartedAtUtc { get; set; }
    public DateTime? LastEventAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? ReconciliationEvidence { get; set; }
    public uint RowVersion { get; set; }
}

// Stored even if callbacks race the HTTP acceptance response. No customer payload is logged.
public sealed class WhatsAppDeliveryEvent : BaseEntity
{
    public string EventKey { get; set; } = "";
    public string AppName { get; set; } = "";
    public string ProviderMessageId { get; set; } = "";
    public string Destination { get; set; } = "";
    public string? WhatsAppMessageId { get; set; }
    public string Type { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}
