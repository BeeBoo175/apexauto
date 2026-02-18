using System.Collections.Concurrent;
using System.Threading.Channels;
using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dealership.UnitTests;

public sealed class MailDeliveryEventTests
{
    [Fact]
    public async Task StartupDrainsBacklogThenWaitsForAnEventWithoutPolling()
    {
        var source = new EventSource();
        for (var index = 0; index < 31; index++)
        {
            source.Enqueue();
        }

        using var services = new ServiceCollection().AddSingleton<IMailDeliverySource>(source).BuildServiceProvider();
        var signal = new MailDeliverySignal();
        using var worker = new MailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            new EventTransport(), signal, NullLogger<MailDeliveryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);

        try
        {
            for (var index = 0; index < 31; index++)
            {
                await source.Delivered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }

            await source.Idle.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var reads = source.Reads;
            // Cross the former poll interval to detect recurring empty database scans.
            await Task.Delay(TimeSpan.FromMilliseconds(5200));
            Assert.Equal(reads, source.Reads);

            source.Enqueue();
            signal.Request();
            await source.Delivered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WakeRequestsCoalesceAndIdleWaitCanBeCancelled()
    {
        var signal = new MailDeliverySignal();
        for (var index = 0; index < 100; index++)
        {
            signal.Request();
        }

        await signal.WaitAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = signal.WaitAsync(cancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    private sealed class EventSource : IMailDeliverySource
    {
        private readonly ConcurrentQueue<ClaimedMail> _pending = new();
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);
        public Channel<bool> Delivered { get; } = Channel.CreateUnbounded<bool>();
        public TaskCompletionSource Idle { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Enqueue()
        {
            _pending.Enqueue(new ClaimedMail(Guid.NewGuid(), Guid.NewGuid(),
                new OutboundMail("aina@example.invalid", "Account confirmation", "Confirm your account.")));
        }

        public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            if (_pending.TryDequeue(out var mail))
            {
                return Task.FromResult<ClaimedMail?>(mail);
            }

            Idle.TrySetResult();
            return Task.FromResult<ClaimedMail?>(null);
        }

        public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
        {
            Delivered.Writer.TryWrite(true);
            return Task.CompletedTask;
        }
    }

    private sealed class EventTransport : IMailTransport
    {
        public bool Enabled => true;

        public void Validate()
        {
        }

        public Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
