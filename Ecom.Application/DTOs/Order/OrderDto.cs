using Ecom.Application.DTOs.Common;
using Ecom.Domain.constant;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Order
{
    public class OrderDto : BaseDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string? AppUserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        
        // Coupon information
        public int? CouponId { get; set; }
        public string? CouponCode { get; set; }
        public decimal? CouponDiscountAmount { get; set; }
        
        public List<OrderItemDto> Items { get; set; } = new();
        public ShippingAddressDto ShippingAddress { get; set; } = new();
    }

    public class OrderCreateDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one order item is required.")]
        public List<OrderItemCreateDto> Items { get; set; } = new();

        [Required]
        public ShippingAddressCreateDto ShippingAddress { get; set; } = new();
        public string? CouponCode { get; set; }

        [EnumDataType(typeof(PaymentMethod))]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Bank;
    }

    public class OrderUpdateStatusDto
    {
        public int Id { get; set; }
        public OrderStatus Status { get; set; }
    }

    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}





