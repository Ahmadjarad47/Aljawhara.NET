using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AdminDashboardController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var data = await _analyticsService.GetDashboardSummaryAsync();
            return Ok(data);
        }

        [HttpGet("last-orders")]
        public async Task<IActionResult> GetLastOrders([FromQuery] int count = 3)
        {
            IEnumerable<Application.DTOs.Order.OrderSummaryDto>? orders = await _analyticsService.GetLastOrdersAsync(count);
            return Ok(orders);
        }

        [HttpGet("last-users")]
        public async Task<IActionResult> GetLastUsers([FromQuery] int count = 3)
        {
            IEnumerable<Application.DTOs.Analytics.UserSummaryDto>? users = await _analyticsService.GetLastRegisteredUsersAsync(count);
            return Ok(users);
        }

        [HttpGet("visitors/chart")]
        public async Task<IActionResult> GetVisitorsChart([FromQuery] int days = 30, [FromQuery] string? period = null)
        {
            days = ParsePeriodToDays(period) ?? days;
            IEnumerable<Application.DTOs.Analytics.TimeSeriesPointDto>? chart = await _analyticsService.GetVisitorsChartAsync(days);
            return Ok(chart);
        }

        [HttpGet("users/chart")]
        public async Task<IActionResult> GetUsersChart([FromQuery] int days = 30, [FromQuery] string? period = null)
        {
            days = ParsePeriodToDays(period) ?? days;
            IEnumerable<Application.DTOs.Analytics.TimeSeriesPointDto>? chart = await _analyticsService.GetUsersChartAsync(days);
            return Ok(chart);
        }

        [HttpGet("orders/chart")]
        public async Task<IActionResult> GetOrdersChart([FromQuery] int days = 30, [FromQuery] string? period = null)
        {
            days = ParsePeriodToDays(period) ?? days;
            IEnumerable<Application.DTOs.Analytics.TimeSeriesPointDto>? chart = await _analyticsService.GetOrdersChartAsync(days);
            return Ok(chart);
        }

        [HttpGet("transactions/chart")]
        public async Task<IActionResult> GetTransactionsChart([FromQuery] int days = 30, [FromQuery] string? period = null)
        {
            days = ParsePeriodToDays(period) ?? days;
            IEnumerable<Application.DTOs.Analytics.TimeSeriesPointDto>? chart = await _analyticsService.GetTransactionsChartAsync(days);
            return Ok(chart);
        }

        private static int? ParsePeriodToDays(string? period)
        {
            if (string.IsNullOrWhiteSpace(period)) return null;
            var p = period.Trim().ToLower();
            return p switch
            {
                "7d" => 7,
                "30d" => 30,
                "60d" => 60,
                "90d" => 90,
                "180d" => 180,
                "365d" => 365,
                "1y" => 365,
                "2y" => 730,
                _ => int.TryParse(p.Replace("d", string.Empty), out var n) ? n : (int?)null
            };
        }
    }
}


