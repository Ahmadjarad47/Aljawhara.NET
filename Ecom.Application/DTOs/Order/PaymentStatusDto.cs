using Ecom.Application.Payments;
using Ecom.Domain.constant;

namespace Ecom.Application.DTOs.Order;

public sealed class PaymentStatusDto
{
    public int OrderId { get; init; }
    public int? PaymentAttemptId { get; init; }
    public string? MerchantReference { get; init; }
    public PaymentAttemptStatus? AttemptStatus { get; init; }
    public PaymentInitializationStatus InitializationStatus { get; init; }
    public string? PaymentUrl { get; init; }
    public DateTime? ReservationExpiresAtUtc { get; init; }
    public DateTime? LastVerificationAtUtc { get; init; }
    public string? VerificationReasonCode { get; init; }
}
