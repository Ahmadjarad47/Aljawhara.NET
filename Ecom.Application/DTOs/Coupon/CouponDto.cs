using Ecom.Application.DTOs.Common;
using Ecom.Domain.constant;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Coupon
{
    public class CouponDto : BaseDto
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
        public int UsedCount { get; set; }
        public new bool IsActive { get; set; }
        public bool IsSingleUse { get; set; }
        public string? AppUserId { get; set; }
        public string? UserName { get; set; }
        public int RemainingUses { get; set; }
        public bool IsExpired { get; set; }
        public bool IsFullyUsed { get; set; }
    }

    public class CouponCreateDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public CouponType Type { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum order amount must be 0 or greater")]
        public decimal? MinimumOrderAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Maximum discount amount must be 0 or greater")]
        public decimal? MaximumDiscountAmount { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be 1 or greater")]
        public int? UsageLimit { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsSingleUse { get; set; } = false;
        public string? AppUserId { get; set; }
    }

    public class CouponUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public CouponType Type { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Value must be greater than 0")]
        public decimal Value { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum order amount must be 0 or greater")]
        public decimal? MinimumOrderAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Maximum discount amount must be 0 or greater")]
        public decimal? MaximumDiscountAmount { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be 1 or greater")]
        public int? UsageLimit { get; set; }

        public bool IsActive { get; set; }
        public bool IsSingleUse { get; set; }
        public string? AppUserId { get; set; }
    }

    public class CouponValidationDto
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Order amount must be greater than 0")]
        public decimal OrderAmount { get; set; }

        public string? UserId { get; set; }
    }

    public class CouponValidationResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public CouponDto? Coupon { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }

    public class CouponSummaryDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CouponType Type { get; set; }
        public decimal Value { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int UsedCount { get; set; }
        public int? UsageLimit { get; set; }
        public bool IsExpired { get; set; }
        public bool IsFullyUsed { get; set; }
        public bool IsDeleted { get; set; }
    }
}
