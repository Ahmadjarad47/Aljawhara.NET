using Ecom.Application.DTOs.Health;

namespace Ecom.Application.Services.Interfaces
{
    public interface IHealthService
    {
        Task<HealthSummaryDto> CheckAsync();
        Task<IEnumerable<HealthChartPointDto>> GetHealthChartAsync(int minutes = 60);
    }
}


