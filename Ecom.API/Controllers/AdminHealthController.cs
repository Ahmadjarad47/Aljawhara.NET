using Ecom.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/health")]
    [Authorize(Roles = "Admin")]
    public class AdminHealthController : ControllerBase
    {
        private readonly IHealthService _healthService;

        public AdminHealthController(IHealthService healthService)
        {
            _healthService = healthService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            Application.DTOs.Health.HealthSummaryDto? status = await _healthService.CheckAsync();
            return Ok(status);
        }

        [HttpGet("chart")]
        public async Task<IActionResult> GetChart([FromQuery] int minutes = 60)
        {
            var chart = await _healthService.GetHealthChartAsync(minutes);
            return Ok(chart);
        }
    }
}


