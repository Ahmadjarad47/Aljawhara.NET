using Ecom.Application.Services.Interfaces;
using Ecom.Application.DTOs.Health;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Ecom.Application.Services
{
    public class HealthService : IHealthService
    {
        private readonly EcomDbContext _dbContext;
        private readonly IUnitOfWork _unitOfWork;

        public HealthService(EcomDbContext dbContext, IUnitOfWork unitOfWork)
        {
            _dbContext = dbContext;
            _unitOfWork = unitOfWork;
        }

        public async Task<HealthSummaryDto> CheckAsync()
        {
            bool dbOk;
            string? error = null;
            try
            {
                dbOk = await _dbContext.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                dbOk = false;
                error = ex.Message;
            }

            var status = new HealthSummaryDto
            {
                Healthy = dbOk,
                Checks = new HealthChecksDto { Database = dbOk },
                Timestamp = DateTime.UtcNow
            };

            await _unitOfWork.HealthPings.AddAsync(new Domain.Entity.HealthPing
            {
                IsHealthy = dbOk,
                Status = dbOk ? "Healthy" : "Unhealthy",
                Error = error
            });
            await _unitOfWork.SaveChangesAsync();

            return status;
        }

        public async Task<IEnumerable<HealthChartPointDto>> GetHealthChartAsync(int minutes = 60)
        {
            var since = DateTime.UtcNow.AddMinutes(-minutes + 1);
            var pings = await _unitOfWork.HealthPings.FindAsync(h => h.CreatedAt >= since);

            var grouped = pings
                .GroupBy(p => new DateTime(p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day, p.CreatedAt.Hour, p.CreatedAt.Minute, 0, DateTimeKind.Utc))
                .Select(g => new { Minute = g.Key, Healthy = g.Count(x => x.IsHealthy), Unhealthy = g.Count(x => !x.IsHealthy) })
                .ToDictionary(x => x.Minute, x => new { x.Healthy, x.Unhealthy });

            var series = new List<HealthChartPointDto>();
            for (int i = 0; i < minutes; i++)
            {
                var point = new DateTime(since.Year, since.Month, since.Day, since.Hour, since.Minute, 0, DateTimeKind.Utc).AddMinutes(i);
                grouped.TryGetValue(point, out var counts);
                series.Add(new HealthChartPointDto { Date = point, Healthy = counts?.Healthy ?? 0, Unhealthy = counts?.Unhealthy ?? 0 });
            }
            return series;
        }
    }
}


