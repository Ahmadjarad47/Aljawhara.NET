using Ecom.Application.DTOs.Coupon;

namespace Ecom.Application.Services.Interfaces
{
    public interface ICouponService
    {
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
        Task<CouponValidationResultDto> ValidateCouponAsync(CouponValidationDto validationDto);
        Task<CouponDto> ApplyCouponToOrderAsync(string couponCode, decimal orderAmount, string? userId = null);
        Task<bool> IncrementCouponUsageAsync(int couponId);
        Task<IEnumerable<CouponDto>> GetExpiredCouponsAsync();
        Task<bool> CleanupExpiredCouponsAsync();
    }
}
