using Ecom.Application.Payments;
using Ecom.API.Monitoring;

namespace Ecom.API.HostedServices;

/// <summary>
/// Polls Sadad through the same strict verification path used by callbacks and webhooks.
/// </summary>
public class SadadPaymentStatusCheckerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SadadPaymentStatusCheckerService> _logger;
    private readonly MonitoringState _monitoring;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public SadadPaymentStatusCheckerService(
        IServiceProvider serviceProvider,
        ILogger<SadadPaymentStatusCheckerService> logger, MonitoringState monitoring)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _monitoring = monitoring;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _monitoring.RegisterJob("Sadad verification", 900);
        _logger.LogInformation("[Sadad Status Checker] Background service started. Will run every {Minutes} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                

                using var scope = _serviceProvider.CreateScope();
                var verification = scope.ServiceProvider.GetRequiredService<ISadadPaymentVerificationService>();

                _logger.LogInformation("[Sadad Status Checker] Running pending payments check.");
                await verification.VerifyPendingBatchAsync($"status-{Environment.MachineName}", 50, stoppingToken);
                _monitoring.JobCompleted("Sadad verification", true);
                _logger.LogInformation("[Sadad Status Checker] Pending payments check completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _monitoring.JobCompleted("Sadad verification", false);
                _logger.LogError(ex, "[Sadad Status Checker] Error during pending payments check.");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }

        _logger.LogInformation("[Sadad Status Checker] Background service stopped.");
    }
}
