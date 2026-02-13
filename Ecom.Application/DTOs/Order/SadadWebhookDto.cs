using System.Text.Json.Serialization;

namespace Ecom.Application.DTOs.Order
{
    /// <summary>
    /// DTO for Sadad paid webhook payload.
    /// Sadad sends this when an invoice is paid.
    /// </summary>
    public class SadadWebhookDto
    {
        [JsonPropertyName("invoiceId")]
        public long InvoiceId { get; set; }      // <-- long æáíÓ string

        [JsonPropertyName("invoiceCode")]
        public string? InvoiceCode { get; set; }

        [JsonPropertyName("invoiceKey")]
        public string? InvoiceKey { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("transactionId")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("transactionStatus")]
        public string? TransactionStatus { get; set; }
    }
}
