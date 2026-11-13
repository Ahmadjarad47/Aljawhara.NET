using Ecom.Application.Telegram;

namespace Ecom.API.HostedServices;

public sealed class TelegramOrderNotificationWorker(
    IServiceScopeFactory scopes,
    ILogger<TelegramOrderNotificationWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"telegram-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                processed = await scope.ServiceProvider
                    .GetRequiredService<TelegramOrderNotificationProcessor>()
                    .ProcessBatchAsync(_workerId, 10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Telegram notification worker iteration failed; queued messages will be retried. FailureType={FailureType}",
                    ex.GetType().Name);
            }

            try
            {
                await Task.Delay(processed > 0 ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
