using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Dealership.IntegrationTests;

internal sealed class PhotoDownloadProbe : IStartupFilter
{
    private readonly DeadlineClock _clock = new();
    private int _stalled;
    private int _completed;

    public TimeProvider Clock => _clock;
    public ConcurrentQueue<Stream> Files { get; } = new();
    public TaskCompletionSource AllStalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ExpireDeadlines()
    {
        _clock.Expire();
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(StallDownloadAsync);
            next(app);
        };
    }

    private async Task StallDownloadAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method != "GET" || !context.Request.Path.StartsWithSegments("/api/intake/photos") ||
            context.Request.Path.Value!.EndsWith("/receipt", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var original = context.Response.Body;
        using var stalled = new StalledStream(original, context, this);
        context.Response.Body = stalled;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;

            if (stalled.WasStalled && Interlocked.Increment(ref _completed) == 4)
            {
                AllCompleted.TrySetResult();
            }
        }
    }

    private sealed class StalledStream(Stream destination, HttpContext context, PhotoDownloadProbe probe) : Stream
    {
        public bool WasStalled { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (context.Response.ContentType?.StartsWith("image/", StringComparison.Ordinal) != true ||
                probe.Release.Task.IsCompleted || buffer.Length == 0)
            {
                await destination.WriteAsync(buffer, cancellationToken);
                return;
            }

            await destination.WriteAsync(buffer[..1], cancellationToken);
            WasStalled = true;

            if (Interlocked.Increment(ref probe._stalled) == 4)
            {
                probe.AllStalled.TrySetResult();
            }

            await probe.Release.Task.WaitAsync(cancellationToken);
            await destination.WriteAsync(buffer[1..], cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override void Flush()
        {
            destination.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return destination.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private readonly ConcurrentQueue<DeadlineTimer> _timers = new();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != TimeSpan.FromSeconds(40))
            {
                return System.CreateTimer(callback, state, dueTime, period);
            }

            var timer = new DeadlineTimer(callback, state);
            _timers.Enqueue(timer);
            return timer;
        }

        public void Expire()
        {
            foreach (var timer in _timers)
            {
                timer.Fire();
            }
        }
    }

    private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;

        public void Fire()
        {
            if (!_disposed)
            {
                callback(state);
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;
        public void Dispose()
        {
            _disposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
