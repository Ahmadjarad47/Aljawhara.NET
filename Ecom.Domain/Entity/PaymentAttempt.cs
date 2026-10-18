using Ecom.Domain.comman;
using Ecom.Domain.constant;

namespace Ecom.Domain.Entity;

public class PaymentAttempt : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string MerchantReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KWD";
    public PaymentAttemptStatus Status { get; set; } = PaymentAttemptStatus.PendingDispatch;
    public string? GatewayInvoiceId { get; set; }
    public string? PaymentUrl { get; set; }
    public int DispatchCount { get; set; }
    public DateTime? LastDispatchStartedAtUtc { get; set; }
    public DateTime? NextActionAtUtc { get; set; }
    public DateTime? GatewayVerifiedAtUtc { get; set; }
    public DateTime? LastVerificationAtUtc { get; set; }
    public string? VerificationEvidenceId { get; set; }
    public string? GatewayTransactionId { get; set; }
    public decimal? VerifiedAmount { get; set; }
    public decimal? VerifiedTotal { get; set; }
    public decimal? VerifiedSubtotal { get; set; }
    public decimal? VerifiedRefundedAmount { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
