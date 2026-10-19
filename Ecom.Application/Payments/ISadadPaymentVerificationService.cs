namespace Ecom.Application.Payments;

public enum PaymentVerificationOutcome
{
    Paid,
    AlreadyPaid,
    NotPaid,
    Rejected,
    RequiresReconciliation,
    Busy,
    NotFound
}

public sealed record PaymentVerificationResult(PaymentVerificationOutcome Outcome, string ReasonCode)
{
    public bool IsAcknowledged => Outcome is PaymentVerificationOutcome.Paid
        or PaymentVerificationOutcome.AlreadyPaid
        or PaymentVerificationOutcome.NotPaid;
}

public interface ISadadPaymentVerificationService
{
    Task<PaymentVerificationResult> VerifyByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyByInvoiceIdAsync(
        string gatewayInvoiceId,
        string triggerId,
        CancellationToken cancellationToken = default);

    Task<int> VerifyPendingBatchAsync(
        string workerId,
        int batchSize,
        CancellationToken cancellationToken = default);
}
