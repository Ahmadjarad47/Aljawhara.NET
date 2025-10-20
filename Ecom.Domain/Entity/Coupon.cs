using Ecom.Domain.comman;
using Ecom.Domain.constant;
using System;

namespace Ecom.Domain.Entity
{
    public class Coupon : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CouponType Type { get; set; }
        public decimal Value { get; set; }
        public decimal? MinimumOrderAmount { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool IsSingleUse { get; set; } = false;
        public string? AppUserId { get; set; } // For user-specific coupons
        public AppUsers? AppUser { get; set; }

        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
