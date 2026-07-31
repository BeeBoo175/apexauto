using System.Threading.RateLimiting;

namespace Dealership.Api.Configuration;

public sealed class PictureDownloadRateLimiter(RateLimitingSettings settings) : RateLimiter
{
    private readonly ConcurrencyLimiter _limiter = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = settings.PictureDownloadConcurrency,
        QueueLimit = settings.PictureDownloadQueueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    public override TimeSpan? IdleDuration => _limiter.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _limiter.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => _limiter.AttemptAcquire(permitCount);

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.PictureDownloadQueueSeconds));

        try
        {
            return await _limiter.AcquireAsync(permitCount, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Admission expires independently of the transfer deadline, which starts only after a lease is granted.
            return new RejectedLease();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _limiter.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore() => _limiter.DisposeAsync();

    private sealed class RejectedLease : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
