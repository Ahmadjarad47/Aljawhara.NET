using AutoMapper;
using Ecom.Application.Services.Interfaces;
using Ecom.Application.DTOs.Order;
using Ecom.Application.DTOs.Analytics;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Ecom.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Application.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<AppUsers> _userManager;

        public AnalyticsService(IUnitOfWork unitOfWork, IMapper mapper, UserManager<AppUsers> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);
            var endOfLastMonth = startOfThisMonth.AddTicks(-1);

            // Users
            var totalUsers = await _userManager.Users.CountAsync();
            var usersLastMonth = await _userManager.Users
                .Where(u => u.CreatedAt >= startOfLastMonth && u.CreatedAt <= endOfLastMonth)
                .CountAsync();

            // Orders
            var totalOrders = await _unitOfWork.Orders.CountAsync();
            var ordersLastMonth = await _unitOfWork.Orders.CountAsync(o => o.CreatedAt >= startOfLastMonth && o.CreatedAt <= endOfLastMonth);

            // Transactions
            var totalTransactions = await _unitOfWork.Transactions.CountAsync();
            var transactionsLastMonth = await _unitOfWork.Transactions.CountAsync(t => t.CreatedAt >= startOfLastMonth && t.CreatedAt <= endOfLastMonth);

            // Visitors
            var totalVisitors = await _unitOfWork.Visitors.CountAsync();
            var visitorsLastMonth = await _unitOfWork.Visitors.CountAsync(v => v.VisitedAtUtc >= startOfLastMonth && v.VisitedAtUtc <= endOfLastMonth);

            // Sales
            var totalSales = await _unitOfWork.Orders.GetTotalSalesAsync(null, null);
            var lastMonthSales = await _unitOfWork.Orders.GetTotalSalesAsync(startOfLastMonth, endOfLastMonth);

            return new DashboardSummaryDto
            {
                Users = new CountSummaryDto { Total = totalUsers, LastMonth = usersLastMonth },
                Orders = new CountSummaryDto { Total = totalOrders, LastMonth = ordersLastMonth },
                Transactions = new CountSummaryDto { Total = totalTransactions, LastMonth = transactionsLastMonth },
                Visitors = new CountSummaryDto { Total = totalVisitors, LastMonth = visitorsLastMonth },
                Sales = new SalesSummaryDto { Total = totalSales, LastMonth = lastMonthSales }
            };
        }

        public async Task<IEnumerable<OrderSummaryDto>> GetLastOrdersAsync(int count = 3)
        {
            var recent = await _unitOfWork.Orders.GetRecentOrdersAsync(count);
            return _mapper.Map<IEnumerable<OrderSummaryDto>>(recent);
        }

        public async Task<IEnumerable<UserSummaryDto>> GetLastRegisteredUsersAsync(int count = 3)
        {
            var users = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(count)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Username = u.UserName,
                    Email = u.Email,
                    CreatedAt = u.CreatedAt,
                    EmailConfirmed = u.EmailConfirmed
                })
                .ToListAsync();

            return users;
        }

        public async Task<IEnumerable<TimeSeriesPointDto>> GetVisitorsChartAsync(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            var visitors = await _unitOfWork.Visitors.FindAsync(v => v.VisitedAtUtc >= since);

            var grouped = visitors
                .GroupBy(v => v.VisitedAtUtc.Date)
                .Select(g => new TimeSeriesPointDto { Date = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Date, x => x.Count);

            var series = new List<TimeSeriesPointDto>();
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i).Date;
                grouped.TryGetValue(day, out var count);
                series.Add(new TimeSeriesPointDto { Date = day, Count = count });
            }

            return series;
        }

        public async Task<IEnumerable<TimeSeriesPointDto>> GetUsersChartAsync(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            var query = _userManager.Users.Where(u => u.CreatedAt >= since);
            var users = await query
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new TimeSeriesPointDto { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var map = users.ToDictionary(x => x.Date, x => x.Count);
            var series = new List<TimeSeriesPointDto>();
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i).Date;
                map.TryGetValue(day, out var count);
                series.Add(new TimeSeriesPointDto { Date = day, Count = count });
            }
            return series;
        }

        public async Task<IEnumerable<TimeSeriesPointDto>> GetOrdersChartAsync(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            var orders = await _unitOfWork.Orders.FindAsync(o => o.CreatedAt >= since);
            var grouped = orders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new TimeSeriesPointDto { Date = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Date, x => x.Count);

            var series = new List<TimeSeriesPointDto>();
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i).Date;
                grouped.TryGetValue(day, out var count);
                series.Add(new TimeSeriesPointDto { Date = day, Count = count });
            }
            return series;
        }

        public async Task<IEnumerable<TimeSeriesPointDto>> GetTransactionsChartAsync(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            var transactions = await _unitOfWork.Transactions.FindAsync(t => t.CreatedAt >= since);
            var grouped = transactions
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new TimeSeriesPointDto { Date = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Date, x => x.Count);

            var series = new List<TimeSeriesPointDto>();
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i).Date;
                grouped.TryGetValue(day, out var count);
                series.Add(new TimeSeriesPointDto { Date = day, Count = count });
            }
            return series;
        }
    }
}


