using System.Text.Json.Serialization;

namespace Ecom.Application.DTOs.Order
{
    /// <summary>
    /// DTO for Sadad paid webhook payload.
    /// Sadad sends this when an invoice is paid.
    /// </summary>
    public class SadadWebhookDto
    {
        public long InvoiceId { get; set; }
        public string InvoiceCode { get; set; }
        public long InvoiceKey { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public string Session_Id { get; set; }
        public string TransactionStatus { get; set; }
    }
}
