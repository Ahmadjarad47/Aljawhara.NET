using Ecom.Application.DTOs.Order;
using Ecom.Application.DTOs.Analytics;

namespace Ecom.Application.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();
        Task<IEnumerable<OrderSummaryDto>> GetLastOrdersAsync(int count = 3);
        Task<IEnumerable<TimeSeriesPointDto>> GetVisitorsChartAsync(int days = 30);
        Task<IEnumerable<UserSummaryDto>> GetLastRegisteredUsersAsync(int count = 3);
        Task<IEnumerable<TimeSeriesPointDto>> GetUsersChartAsync(int days = 30);
        Task<IEnumerable<TimeSeriesPointDto>> GetOrdersChartAsync(int days = 30);
        Task<IEnumerable<TimeSeriesPointDto>> GetTransactionsChartAsync(int days = 30);
    }
}


