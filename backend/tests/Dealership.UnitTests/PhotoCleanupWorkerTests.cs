using System.Collections.Concurrent;
using System.Threading.Channels;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Photos;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Photos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dealership.UnitTests;

public sealed class PhotoCleanupWorkerTests
{
    [Fact]
    public async Task OneWorkerProcessesBothSourcesOnStartupThenWaitsForPhotoEvents()
    {
        var intake = new CleanupSource();
        var inspection = new CleanupSource();
        intake.Enqueue();
        inspection.Enqueue();
        using var services = CreateServices(intake, inspection);
        var signal = new PhotoCleanupSignal();
        using var worker = Worker(services, signal);
        await worker.StartAsync(CancellationToken.None);

        try
        {
            await DeletedAsync(intake);
            await DeletedAsync(inspection);
            var reads = intake.Reads + inspection.Reads;
            // Cross the former poll interval to detect recurring empty database scans.
            await Task.Delay(TimeSpan.FromMilliseconds(5200));
            Assert.Equal(reads, intake.Reads + inspection.Reads);

            inspection.Enqueue();
            signal.Request();
            await DeletedAsync(inspection);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FailureInOneSourceDoesNotPreventTheOtherFromCleaningUp()
    {
        var unavailable = new CleanupSource { FailScan = true };
        var available = new CleanupSource();
        available.Enqueue();
        using var services = CreateServices(unavailable, available);
        using var worker = Worker(services, new PhotoCleanupSignal());

        await worker.RunBatchAsync(CancellationToken.None);

        await DeletedAsync(available);
        Assert.Equal(1, unavailable.Reads);
    }

    [Fact]
    public async Task DisabledCleanupDoesNotScanOnStartupOrEvents()
    {
        var source = new CleanupSource();
        using var services = CreateServices(source);
        var signal = new PhotoCleanupSignal();
        signal.Request();
        using var worker = Worker(services, signal, enabled: false);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.Equal(0, source.Reads);
    }

    private static PhotoCleanupWorker Worker(ServiceProvider services, PhotoCleanupSignal signal, bool enabled = true)
    {
        return new PhotoCleanupWorker(services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IntakePhotoSettings { CleanupEnabled = enabled }), signal,
            NullLogger<PhotoCleanupWorker>.Instance);
    }

    private static ServiceProvider CreateServices(params CleanupSource[] sources)
    {
        var services = new ServiceCollection();
        foreach (var source in sources)
        {
            services.AddSingleton<IPhotoCleanupSource>(source);
        }

        services.AddSingleton<IPhotoStorage, CleanupStorage>();
        services.AddSingleton<IPhotoTransferLock, TransferLock>();
        return services.BuildServiceProvider();
    }

    private static async Task DeletedAsync(CleanupSource source)
    {
        await source.Deleted.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class CleanupSource : IPhotoCleanupSource
    {
        private readonly ConcurrentDictionary<Guid, PhotoDeletionClaim> _pending = new();
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);
        public bool FailScan { get; init; }
        public Channel<bool> Deleted { get; } = Channel.CreateUnbounded<bool>();

        public void Enqueue()
        {
            var id = Guid.NewGuid();
            _pending[id] = new PhotoDeletionClaim(new PhotoObject(id.ToString()), Guid.NewGuid());
        }

        public Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            if (FailScan)
            {
                throw new IOException("Simulated cleanup scan failure.");
            }

            return Task.FromResult<IReadOnlyList<Guid>>([.. _pending.Keys]);
        }

        public Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_pending.GetValueOrDefault(id));
        }

        public Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken)
        {
            Assert.Equal(_pending[photoId].ClaimId, claimId);
            _pending.TryRemove(photoId, out _);
            Deleted.Writer.TryWrite(true);
            return Task.CompletedTask;
        }
    }

    private sealed record PhotoObject(string ObjectKey) : IPhotoObject
    {
        public string ContentType => "image/jpeg";
        public long Bytes => 1;
        public string Sha256 => string.Empty;
    }

    private sealed class CleanupStorage : IPhotoStorage
    {
        public Task PutAsync(IPhotoObject photo, Stream content, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task CopyToAsync(IPhotoObject photo, Stream destination, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(IPhotoObject photo, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TransferLock : IPhotoTransferLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid photoId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncDisposable>(new Lease());
        }

        public Task<IAsyncDisposable?> TryAcquireAsync(Guid photoId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncDisposable?>(new Lease());
        }

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
