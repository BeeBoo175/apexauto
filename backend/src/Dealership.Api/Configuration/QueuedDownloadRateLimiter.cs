using System.Threading.RateLimiting;

namespace Dealership.Api.Configuration;

// Image reads wait briefly for a slot, because one page asks for many at once; a plain concurrency cap rejects them.
public sealed class QueuedDownloadRateLimiter(int concurrency, int queueLimit, TimeSpan queueWait) : RateLimiter
{
    private readonly ConcurrencyLimiter _limiter = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = concurrency,
        QueueLimit = queueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    public override TimeSpan? IdleDuration => _limiter.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _limiter.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => _limiter.AttemptAcquire(permitCount);

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(queueWait);

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

        base.Dispose(disposing);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _limiter.DisposeAsync();
        await base.DisposeAsyncCore();
    }

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
