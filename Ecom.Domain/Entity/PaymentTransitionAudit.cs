using Ecom.Domain.comman;
using Ecom.Domain.constant;

namespace Ecom.Domain.Entity;

public class PaymentTransitionAudit : BaseEntity
{
    public int PaymentAttemptId { get; set; }
    public PaymentAttempt PaymentAttempt { get; set; } = null!;
    public PaymentAttemptStatus FromStatus { get; set; }
    public PaymentAttemptStatus ToStatus { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
}
