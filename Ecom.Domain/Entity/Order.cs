using Ecom.Domain.comman;
using Ecom.Domain.constant;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ecom.Domain.Entity
{
    public class Order : BaseEntity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Tax { get; set; }
        public decimal? Discount { get; set; }
        public decimal Total { get; set; }
        public PaymentMethod PaymentMethod { get; set; }

        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        // Foreign key to ShippingAddress
        public int ShippingAddressId { get; set; }
        public ShippingAddress ShippingAddress { get; set; } = null!;

        // Optional relation to the user who made the order
        public string? AppUserId { get; set; }
        public AppUsers? AppUser { get; set; }
    }
}
