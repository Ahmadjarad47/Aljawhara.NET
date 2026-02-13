using Ecom.Domain.comman;
using Ecom.Domain.constant;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class Transaction : BaseEntity
    {
        // Foreign key to Order
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        // Foreign key to AppUser
        public string? AppUserId { get; set; }
        public AppUsers? AppUser { get; set; }

        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedDate { get; set; }
        public string? TransactionReference { get; set; }
        public string? PaymentGatewayResponse { get; set; }
        public string? Notes { get; set; }
        public bool IsRefunded { get; set; } = false;
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundDate { get; set; }
        public string? RefundReason { get; set; }
        public string? GatewayInvoiceId { get; set; }
    }
}
