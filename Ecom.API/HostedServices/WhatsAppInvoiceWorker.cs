using Ecom.Application.WhatsApp;

namespace Ecom.API.HostedServices;

public sealed class WhatsAppInvoiceWorker(IServiceScopeFactory scopes, ILogger<WhatsAppInvoiceWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"whatsapp-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WhatsAppInvoiceProcessor>().ProcessBatchAsync(_workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "WhatsApp invoice worker failed; persisted jobs will be recovered."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
