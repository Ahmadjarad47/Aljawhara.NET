using Ecom.Domain.Entity;
using Ecom.Domain.constant;

namespace Ecom.Infrastructure.Repositories.Interfaces
{
    public interface IOrderRepository : IBaseRepository<Order>
    {
        Task<IEnumerable<Order>> GetOrdersByUserAsync(string userId);
        Task<Order?> GetOrderWithItemsAsync(int orderId);
        Task<Order?> GetOrderWithItemsForUpdateAsync(int orderId);
        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetOrderCountByStatusAsync(OrderStatus status);
        Task<IEnumerable<Order>> GetRecentOrdersAsync(int count = 10);
    }
}

