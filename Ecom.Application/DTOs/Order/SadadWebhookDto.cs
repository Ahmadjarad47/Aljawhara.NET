using System.Text.Json.Serialization;

namespace Ecom.Application.DTOs.Order
{
    /// <summary>
    /// DTO for Sadad paid webhook payload.
    /// Sadad sends this when an invoice is paid.
    /// </summary>
    public class SadadWebhookDto
    {
        [JsonPropertyName("InvoiceId")]
        public string? InvoiceId { get; set; }

        [JsonPropertyName("InvoiceCode")]
        public string? InvoiceCode { get; set; }

        [JsonPropertyName("InvoiceKey")]
        public string? InvoiceKey { get; set; }

        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        [JsonPropertyName("Amount")]
        public string? Amount { get; set; }

        [JsonPropertyName("PaymentMethod")]
        public string? PaymentMethod { get; set; }

        [JsonPropertyName("TransactionId")]
        public string? TransactionId { get; set; }
    }
}
