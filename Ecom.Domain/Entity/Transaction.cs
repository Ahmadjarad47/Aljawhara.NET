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
        public PaymentMethod PaymentMethod { get; set; }  // e.g., CreditCard, PayPal
        public string Status { get; set; } = string.Empty;               // e.g., Pending, Completed, Failed
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    }
}
