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
        public decimal Total { get; set; }
        public DateTime? WhatsAppConsentAtUtc { get; set; }
        public string? WhatsAppConsentPhone { get; set; }
        public string? WhatsAppConsentSource { get; set; }

        // Coupon relationship
        public int? CouponId { get; set; }
        public Coupon? Coupon { get; set; }
        public decimal? CouponDiscountAmount { get; set; }

        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        // Foreign key to ShippingAddress
        public int ShippingAddressId { get; set; }
        public ShippingAddress ShippingAddress { get; set; } = null!;

        // Optional relation to the user who made the order
        public string? AppUserId { get; set; }
        public AppUsers? AppUser { get; set; }

        // Guest checkout capability. Only a SHA-256 hash is persisted; the raw
        // order-scoped secret is returned once to the guest at creation time.
        public string? GuestAccessTokenHash { get; set; }
        public DateTime? GuestAccessExpiresAtUtc { get; set; }
        public DateTime? GuestAccessRevokedAtUtc { get; set; }
        public string? CheckoutIdempotencyKeyHash { get; set; }
        public string? CheckoutRequestHash { get; set; }
        public string? GuestTokenDerivationNonce { get; set; }
        public DateTime? ReservationExpiresAtUtc { get; set; }
        public List<InventoryReservation> InventoryReservations { get; set; } = new();
        public CouponReservation? CouponReservation { get; set; }
        public List<PaymentAttempt> PaymentAttempts { get; set; } = new();
    }
}
