namespace Ecom.Domain.constant;

public enum PaymentAttemptStatus
{
    PendingDispatch = 0,
    Dispatching = 1,
    AwaitingPayment = 2,
    RequiresReconciliation = 3,
    Paid = 4,
    Failed = 5,
    Cancelled = 6,
    Expired = 7
}
