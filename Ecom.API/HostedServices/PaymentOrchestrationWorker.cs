using Ecom.Application.Payments;

namespace Ecom.API.HostedServices;

public sealed class PaymentOrchestrationWorker(IServiceScopeFactory scopes, ILogger<PaymentOrchestrationWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IPaymentOrchestrationService>();
                var count = await processor.ProcessOutboxBatchAsync(_workerId, 20, stoppingToken);
                await processor.ExpireReservationsAsync(stoppingToken);
                await Task.Delay(count == 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Payment orchestration worker iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
