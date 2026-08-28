using Dealership.Application.Vehicles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure.Vehicles;

// A deposit request that lapses or is cancelled unread keeps its unpaid bill payable; this removes those bills shortly
// after, so a customer cannot pay for a car they no longer hold.
public sealed class DepositBillSweepWorker(IServiceScopeFactory scopes, ILogger<DepositBillSweepWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly Action<ILogger, string, Exception?> SweepFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(4, nameof(SweepFailure)),
        "Deposit bill sweep remains retryable ({FailureType}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ReservationPaymentService>()
                .ReleaseLapsedBillsAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SweepFailure(logger, exception.GetType().Name, null);
        }
    }
}
