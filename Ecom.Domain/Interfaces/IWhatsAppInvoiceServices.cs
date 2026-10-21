namespace Ecom.Domain.Interfaces;

public sealed record InvoiceLine(string Name, int Quantity, decimal Price);
public sealed record InvoiceData(string InvoiceNumber, string OrderNumber, string CustomerName,
    DateTime PaidAtUtc, IReadOnlyList<InvoiceLine> Lines, decimal Subtotal, decimal Discount,
    decimal Shipping, decimal Tax, decimal Total);

public interface IInvoicePdfRenderer
{
    Task<byte[]> RenderAsync(InvoiceData invoice, CancellationToken cancellationToken);
}

public enum WhatsAppSendOutcome { Accepted, Rejected, RetryableRejection, Unknown }
public sealed record WhatsAppSendResult(WhatsAppSendOutcome Outcome, string? MessageId = null, string? ErrorCode = null);

public interface IGupshupWhatsAppService
{
    Task<WhatsAppSendResult> SendInvoiceAsync(string destination, string orderNumber, string mediaUrl,
        CancellationToken cancellationToken);
    Task<WhatsAppSendResult> SendPhoneVerificationCodeAsync(string destination, string code,
        CancellationToken cancellationToken);
}
