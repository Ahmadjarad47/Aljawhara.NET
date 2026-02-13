using Ecom.Application.Services.Interfaces;

namespace Ecom.API.HostedServices;

/// <summary>
/// Background service that runs every 5 minutes to check pending Sadad transactions
/// and update their status if payment was completed.
/// </summary>
public class SadadPaymentStatusCheckerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SadadPaymentStatusCheckerService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public SadadPaymentStatusCheckerService(
        IServiceProvider serviceProvider,
        ILogger<SadadPaymentStatusCheckerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Sadad Status Checker] Background service started. Will run every {Minutes} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService>();

                _logger.LogInformation("[Sadad Status Checker] Running pending payments check.");
                await transactionService.CheckPendingSadadPaymentsAsync();
                _logger.LogInformation("[Sadad Status Checker] Pending payments check completed.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Sadad Status Checker] Error during pending payments check.");
            }
        }

        _logger.LogInformation("[Sadad Status Checker] Background service stopped.");
    }
}
