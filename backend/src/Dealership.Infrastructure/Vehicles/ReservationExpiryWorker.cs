using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, ReservationExpirySignal signal,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> ExpiryFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(3, nameof(ExpiryFailure)),
        "Reservation expiry remains retryable ({FailureType}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        signal.Request();
        while (!stoppingToken.IsCancellationRequested)
        {
            // Reads/actions enforce deadlines; idle catch-up waits for activity or restart.
            await signal.WaitAsync(stoppingToken);
            await RunBatchAsync(stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var caughtUp = await ReservationReadSnapshot.ReadAsync<bool?>(database,
                async () => await ReservationReadSnapshot.ReconcileDueAsync(database,
                    database.Vehicles.Select(item => item.Id), 10, deadline.Token), null, deadline.Token);
            if (caughtUp is null)
            {
                ExpiryFailure(logger, "SerializationFailure", null);
                return;
            }

            if (!caughtUp.Value)
            {
                // The committed batch made progress; drain remaining due work without another event.
                signal.Request();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ExpiryFailure(logger, exception.GetType().Name, null);
        }
    }
}
