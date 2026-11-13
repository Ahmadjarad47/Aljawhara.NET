using Ecom.Application.DTOs.Coupon;

namespace Ecom.Application.Services.Interfaces
{
    public interface ICouponService
    {
        Task<IReadOnlyList<StorefrontCouponDto>> GetPublicCouponsAsync();
        Task<StorefrontCouponDto?> GetVisibleCouponAsync(int? id, string? code, string? userId);
        Task<IReadOnlyList<StorefrontCouponDto>> GetOwnedCouponsAsync(string userId);
        Task<StorefrontCouponValidationDto> PreviewCouponAsync(CouponValidationDto validation, string? userId);
        Task<CouponDto?> GetCouponByIdAsync(int id);
        Task<CouponDto?> GetCouponByCodeAsync(string code);
        Task<IEnumerable<CouponDto>> GetAllCouponsAsync();
        Task<IEnumerable<CouponDto>> GetActiveCouponsAsync();
        Task<IEnumerable<CouponDto>> GetCouponsByUserAsync(string userId);
        Task<IEnumerable<CouponSummaryDto>> GetCouponSummariesAsync();
        Task<CouponDto> CreateCouponAsync(CouponCreateDto couponDto);
        Task<CouponDto> UpdateCouponAsync(CouponUpdateDto couponDto);
        Task<bool> DeleteCouponAsync(int id);
        Task<bool> ActivateCouponAsync(int id);
        Task<bool> DeactivateCouponAsync(int id);
        Task<CouponValidationResultDto> ValidateCouponAsync(CouponValidationDto validationDto, string? userId);
        Task<CouponDto> ApplyCouponToOrderAsync(string couponCode, decimal orderAmount, string? userId = null);
        Task<bool> IncrementCouponUsageAsync(int couponId);
        Task<IEnumerable<CouponDto>> GetExpiredCouponsAsync();
        Task<bool> CleanupExpiredCouponsAsync();
    }
}
