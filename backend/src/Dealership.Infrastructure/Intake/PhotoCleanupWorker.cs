using Dealership.Application.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Intake;

public sealed class PhotoCleanupWorker(IServiceScopeFactory scopes, IOptions<PhotoUploadSettings> settings,
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
            candidates = await scope.ServiceProvider.GetRequiredService<PhotoCleanupStore>().GetCandidatesAsync(cancellationToken);
        }

        foreach (var id in candidates)
        {
            await DeleteNextAsync(id, cancellationToken);
        }
    }

    private async Task DeleteNextAsync(Guid id, CancellationToken cancellationToken)
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

            var store = services.GetRequiredService<PhotoCleanupStore>();
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
