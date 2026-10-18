namespace Ecom.Application.Payments;

public interface IPaymentOrchestrationService
{
    Task<int> ProcessOutboxBatchAsync(string workerId, int batchSize, CancellationToken cancellationToken = default);
    Task<int> ExpireReservationsAsync(CancellationToken cancellationToken = default);
}

public interface IPaymentFinalizationService
{
    Task<bool> FinalizeVerifiedAsync(int paymentAttemptId, string evidenceId, string gatewayInvoiceId, string merchantReference, string merchantIdentity, decimal amount, string currency, CancellationToken cancellationToken = default);
}
