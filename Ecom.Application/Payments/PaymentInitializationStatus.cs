namespace Ecom.Application.Payments;

public enum PaymentInitializationStatus
{
    NotApplicable = 0,
    Pending = 1,
    Ready = 2,
    RequiresReconciliation = 3,
    Failed = 4,
    Paid = 5
}
