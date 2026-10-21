using Ecom.Domain.comman;

namespace Ecom.Domain.Entity;

public enum WhatsAppConsentAction
{
    Granted,
    Revoked
}

public sealed class WhatsAppConsentAudit : BaseEntity
{
    public string? AppUserId { get; set; }
    public AppUsers? AppUser { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string Phone { get; set; } = "";
    public WhatsAppConsentAction Action { get; set; }
    public string Source { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public string ProviderSyncStatus { get; set; } = "NotRequired";
    public string? ProviderSyncError { get; set; }
}

public sealed class WhatsAppPhoneVerification : BaseEntity
{
    public string AppUserId { get; set; } = "";
    public AppUsers AppUser { get; set; } = null!;
    public string Phone { get; set; } = "";
    public byte[] CodeHash { get; set; } = [];
    public byte[] CodeSalt { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int FailedAttemptCount { get; set; }
    public string? ProviderMessageId { get; set; }
}
