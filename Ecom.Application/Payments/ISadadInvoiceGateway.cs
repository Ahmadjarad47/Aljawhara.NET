using Ecom.Domain.Entity;

namespace Ecom.Application.Payments;

public enum GatewayDispatchOutcome { Created, DefinitiveFailure, RetryableBeforeSend, Unknown }

public sealed record GatewayDispatchResult(GatewayDispatchOutcome Outcome, string? InvoiceId = null, string? PaymentUrl = null, string? ErrorCode = null);

public enum SadadInvoiceVerificationOutcome
{
    VerifiedPaid,
    NotPaid,
    Invalid,
    RequiresReconciliation,
    Unsupported
}

public sealed record SadadVerifiedPayment(
    string EvidenceId,
    string GatewayInvoiceId,
    long VendorId,
    string MerchantReference,
    decimal Amount,
    decimal Total,
    decimal Subtotal,
    decimal RefundedAmount,
    string Currency,
    string? GatewayTransactionId);

public sealed record SadadInvoiceVerificationResult(
    SadadInvoiceVerificationOutcome Outcome,
    string ReasonCode,
    SadadVerifiedPayment? Payment = null);

public enum SadadInvoiceLookupOutcome { FoundUnique, NotFound, Ambiguous, Unavailable, Unsupported }

public sealed record SadadInvoiceLookupResult(
    SadadInvoiceLookupOutcome Outcome,
    string ReasonCode,
    string? InvoiceId = null);

public interface ISadadInvoiceGateway
{
    Task<GatewayDispatchResult> CreateInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken);
    Task<SadadInvoiceVerificationResult> VerifyInvoiceAsync(PaymentAttempt attempt, CancellationToken cancellationToken);
    Task<SadadInvoiceLookupResult> FindInvoiceByReferenceAsync(PaymentAttempt attempt, CancellationToken cancellationToken);
}
