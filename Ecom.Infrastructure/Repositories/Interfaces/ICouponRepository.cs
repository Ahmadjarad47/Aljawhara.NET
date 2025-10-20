using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface ICouponRepository : IBaseRepository<Coupon>
    {
        Task<Coupon?> GetByCodeAsync(string code);
        Task<IEnumerable<Coupon>> GetActiveCouponsAsync();
        Task<IEnumerable<Coupon>> GetCouponsByUserAsync(string userId);
        Task<IEnumerable<Coupon>> GetExpiredCouponsAsync();
        Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null);
        Task IncrementUsageCountAsync(int couponId);
        Task<Coupon?> GetCouponWithDetailsAsync(int id);
    }
}
