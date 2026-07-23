using Ecom.Domain.constant;

namespace Ecom.Application.DTOs.Order
{
    public class InvoicePaymentDto
    {
        // Order Information
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public DateTime OrderCreatedAt { get; set; }
        
        // Financial Information
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal PaymentAmount { get; set; } // Amount to be paid (same as Total)
        
        // Customer Information
        public string? AppUserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        
        // Coupon Information
        public int? CouponId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? CouponDiscountAmount { get; set; }
        
        // Order Items
        public List<OrderItemDto> Items { get; set; } = new();
        
        // Shipping Address
        public ShippingAddressDto ShippingAddress { get; set; } = new();
        
        // Success indicator
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
    }
}

