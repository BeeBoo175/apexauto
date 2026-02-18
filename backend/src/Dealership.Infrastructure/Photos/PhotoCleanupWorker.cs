using Dealership.Application.Photos;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Photos;

public sealed class PhotoCleanupWorker(IServiceScopeFactory scopes, IOptions<IntakePhotoSettings> settings, PhotoCleanupSignal signal,
    ILogger<PhotoCleanupWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> CleanupFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2, nameof(CleanupFailure)),
        "Photo cleanup remains retryable ({FailureType}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.CleanupEnabled)
        {
            return;
        }

        signal.Request();
        while (!stoppingToken.IsCancellationRequested)
        {
            // Free hosting can defer physical cleanup until activity resumes.
            await signal.WaitAsync(stoppingToken);
            await RunBatchAsync(stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        int sourceCount;

        await using (var scope = scopes.CreateAsyncScope())
        {
            sourceCount = scope.ServiceProvider.GetServices<IPhotoCleanupSource>().Count();
        }

        for (var sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
        {
            await RunSourceAsync(sourceIndex, cancellationToken);
        }
    }

    private async Task RunSourceAsync(int sourceIndex, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<Guid> candidates;
            await using (var scope = scopes.CreateAsyncScope())
            {
                candidates = await scope.ServiceProvider.GetServices<IPhotoCleanupSource>().ElementAt(sourceIndex)
                    .GetCandidatesAsync(cancellationToken);
            }

            foreach (var id in candidates)
            {
                await DeleteNextAsync(sourceIndex, id, cancellationToken);
            }
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

    private async Task DeleteNextAsync(int sourceIndex, Guid id, CancellationToken cancellationToken)
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

            var store = services.GetServices<IPhotoCleanupSource>().ElementAt(sourceIndex);
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
