using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class OrderRepository : BaseRepository<Order>, IOrderRepository
    {
        public OrderRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserAsync(string userId)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.AppUser)
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.ShippingAddress)
                .Where(o => o.AppUserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderWithItemsAsync(int orderId)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetOrderWithItemsForUpdateAsync(int orderId)
        {
            // Load with tracking enabled for update operations
            // Don't include Product navigation property to avoid tracking conflicts
            return await _dbSet
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        }

        public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(o => o.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(o => o.CreatedAt <= endDate.Value);

            query = query.Where(o => o.Status == OrderStatus.Delivered);

            return await query.SumAsync(o => o.Total);
        }

        public async Task<int> GetOrderCountByStatusAsync(OrderStatus status)
        {
            return await _dbSet.CountAsync(o => o.Status == status);
        }

        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Items.Where(i => !i.IsDeleted))
                .Include(o => o.ShippingAddress)
                .Include(o => o.AppUser)
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}

