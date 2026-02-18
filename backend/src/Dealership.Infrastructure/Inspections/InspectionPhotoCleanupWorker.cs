using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionPhotoCleanupWorker(IServiceScopeFactory scopes, IOptions<IntakePhotoSettings> settings,
    ILogger<InspectionPhotoCleanupWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> CleanupFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(8, nameof(CleanupFailure)), "Inspection photo cleanup remains retryable ({FailureType}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.CleanupEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                CleanupFailure(logger, exception.GetType().Name, null);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> candidates;
        await using (var scope = scopes.CreateAsyncScope())
        {
            candidates = await scope.ServiceProvider.GetRequiredService<InspectionPhotoCleanupStore>()
                .GetCandidatesAsync(cancellationToken);
        }
        foreach (var id in candidates)
        {
            await DeleteAsync(id, cancellationToken);
        }
    }

    private async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await using var transfer = await services.GetRequiredService<IPhotoTransferLock>().TryAcquireAsync(id, deadline.Token);
            if (transfer is null)
            {
                return;
            }

            var store = services.GetRequiredService<InspectionPhotoCleanupStore>();
            var claim = await store.ClaimAsync(id, deadline.Token);
            if (claim is null)
            {
                return;
            }

            await services.GetRequiredService<IPhotoStorage>().DeleteAsync(claim.Photo, deadline.Token);
            await store.MarkDeletedAsync(id, claim.ClaimId, deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CleanupFailure(logger, exception.GetType().Name, null);
        }
    }
}
