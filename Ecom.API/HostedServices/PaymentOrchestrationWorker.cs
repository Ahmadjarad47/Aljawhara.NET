using Ecom.Application.Payments;
using Ecom.API.Monitoring;

namespace Ecom.API.HostedServices;

public sealed class PaymentOrchestrationWorker(IServiceScopeFactory scopes, ILogger<PaymentOrchestrationWorker> logger,
    MonitoringState monitoring) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        monitoring.RegisterJob("Payment orchestration", 180);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IPaymentOrchestrationService>();
                var count = await processor.ProcessOutboxBatchAsync(_workerId, 20, stoppingToken);
                await processor.ExpireReservationsAsync(stoppingToken);
                monitoring.JobCompleted("Payment orchestration", true);
                if (count > 0) logger.LogInformation("Payment orchestration processed {Count} messages", count);
                await Task.Delay(count == 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                monitoring.JobCompleted("Payment orchestration", false);
                logger.LogError(ex, "Payment orchestration worker iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
