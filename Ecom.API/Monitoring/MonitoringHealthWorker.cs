using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ecom.API.Monitoring;

public sealed class MonitoringHealthWorker(HealthCheckService healthChecks, MonitoringState state,
    IOptions<MonitoringOptions> options, ILogger<MonitoringHealthWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? previousStatus = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var report = await healthChecks.CheckHealthAsync(timeout.Token);
                var status = report.Status.ToString();
                state.SetHealth(new { status, checkedAt = DateTimeOffset.UtcNow,
                    checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(),
                        durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1) }).ToArray() });
                if (status != previousStatus)
                    logger.Log(report.Status == HealthStatus.Healthy ? LogLevel.Information : LogLevel.Error,
                        "System health changed to {Status}", status);
                previousStatus = status;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                state.SetHealth(new { status = "Unhealthy", checkedAt = DateTimeOffset.UtcNow,
                    checks = new[] { new { name = "database", status = "Unhealthy", durationMs = 10000 } } });
                if (previousStatus != "Unhealthy") logger.LogError(ex, "System health check failed");
                previousStatus = "Unhealthy";
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.HealthIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
