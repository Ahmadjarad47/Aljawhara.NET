using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecom.AIShopping.Infrastructure.BackgroundJobs;

public sealed class ShoppingIndexWorker(IServiceScopeFactory scopes, IOptionsMonitor<AIShoppingOptions> options,
    ILogger<ShoppingIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var o = options.CurrentValue;
            try
            {
                if (o.Enabled && o.IndexingEnabled)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ShoppingSearchIndexService>().RunBatchAsync(false, stoppingToken);
                }
                // Retention also runs without catalog indexing; disabling the feature stops all AI work.
                if (o.Enabled && !o.IndexingEnabled)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<Persistence.AIShoppingDbContext>();
                    await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(db.Conversations.Where(c => c.ExpiresAtUtc <= DateTime.UtcNow), stoppingToken);
                    await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(db.ConfirmationReceipts.Where(c => c.ExpiresAtUtc <= DateTime.UtcNow), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("AI background maintenance failed. Code=AI_MAINTENANCE_FAILED"); }
            await Task.Delay(TimeSpan.FromSeconds(o.IndexIntervalSeconds), stoppingToken);
        }
    }
}
