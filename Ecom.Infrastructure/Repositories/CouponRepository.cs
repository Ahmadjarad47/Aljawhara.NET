using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class CouponRepository : BaseRepository<Coupon>, ICouponRepository
    {
        public CouponRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<Coupon?> GetByCodeAsync(string code)
        {
            return await _context.Coupons
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(c => c.Code == code && !c.IsDeleted);
        }

        public async Task<IEnumerable<Coupon>> GetActiveCouponsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Coupons
                .Include(c => c.AppUser)
                .Where(c => c.IsActive && 
                           c.StartDate <= now && 
                           c.EndDate >= now && 
                           !c.IsDeleted)
                .OrderBy(c => c.StartDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Coupon>> GetCouponsByUserAsync(string userId)
        {
            return await _context.Coupons
                .Include(c => c.AppUser)
                .Where(c => c.AppUserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Coupon>> GetExpiredCouponsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Coupons
                .Include(c => c.AppUser)
                .Where(c => c.EndDate < now && !c.IsDeleted)
                .OrderByDescending(c => c.EndDate)
                .ToListAsync();
        }

        public async Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null)
        {
            var query = _context.Coupons.Where(c => c.Code == code && !c.IsDeleted);
            
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task IncrementUsageCountAsync(int couponId)
        {
            var coupon = await _context.Coupons.FindAsync(couponId);
            if (coupon != null)
            {
                coupon.UsedCount++;
                _context.Coupons.Update(coupon);
            }
        }

        public async Task<Coupon?> GetCouponWithDetailsAsync(int id)
        {
            return await _context.Coupons
                .Include(c => c.AppUser)
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }
    }
}
