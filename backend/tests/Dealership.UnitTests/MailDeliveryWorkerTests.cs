using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dealership.UnitTests;

public sealed class MailDeliveryWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackloggedOrFailingQueueCannotStarveAnotherPurpose(bool failFirst)
    {
        var first = new TestSource("verification", 20);
        var second = new TestSource("recovery", 1);
        var transport = new TestTransport { FailRecipient = failFirst ? "verification" : null };
        using var services = CreateServices(first, second);
        using var worker = new MailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            transport, new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);

        await worker.DeliverBatchAsync(CancellationToken.None);

        Assert.Equal(["verification", "recovery"], transport.Attempts.Take(2));
        Assert.Equal(10, first.Remaining);
        Assert.Equal(0, second.Remaining);
        Assert.Equal(1, second.Acknowledged);
        Assert.Equal(failFirst ? 0 : 10, first.Acknowledged);
    }

    [Fact]
    public async Task ClaimFailureDoesNotPreventOtherQueuesFromProgressing()
    {
        var broken = new TestSource("verification", 1) { FailClaim = true };
        var recovery = new TestSource("recovery", 1);
        var transport = new TestTransport();
        using var services = CreateServices(broken, recovery);
        using var worker = new MailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            transport, new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);

        await worker.DeliverBatchAsync(CancellationToken.None);

        Assert.Equal(["recovery"], transport.Attempts);
        Assert.Equal(1, recovery.Acknowledged);
        Assert.Equal(0, broken.Acknowledged);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightSendWithoutAcknowledgingOrStartingMoreMail()
    {
        var first = new TestSource("verification", 1);
        var second = new TestSource("recovery", 1);
        var transport = new TestTransport { BlockSend = true };
        using var services = CreateServices(first, second);
        using var worker = new MailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            transport, new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);
        using var cancellation = new CancellationTokenSource();
        var delivery = worker.DeliverBatchAsync(cancellation.Token);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
        Assert.Equal(0, first.Acknowledged);
        Assert.Equal(1, second.Remaining);
    }

    [Fact]
    public async Task DisabledWorkerDoesNotClaimOrValidateDelivery()
    {
        var queue = new TestSource("verification", 1);
        var transport = new TestTransport { Enabled = false };
        using var services = CreateServices(queue);
        using var worker = new MailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
            transport, new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;

        Assert.Equal(1, queue.Remaining);
        Assert.False(transport.Validated);
    }

    private static ServiceProvider CreateServices(params TestSource[] queues)
    {
        var services = new ServiceCollection();

        foreach (var queue in queues)
        {
            services.AddSingleton<IMailDeliverySource>(queue);
        }

        return services.BuildServiceProvider();
    }

    private sealed class TestSource(string recipient, int remaining) : IMailDeliverySource
    {
        public int Remaining { get; private set; } = remaining;

        public int Acknowledged { get; private set; }

        public bool FailClaim { get; init; }

        public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
        {
            if (FailClaim)
            {
                throw new IOException("Simulated queue outage.");
            }

            if (Remaining == 0)
            {
                return Task.FromResult<ClaimedMail?>(null);
            }

            Remaining--;
            return Task.FromResult<ClaimedMail?>(new ClaimedMail(Guid.NewGuid(), Guid.NewGuid(),
                new OutboundMail(recipient, "Account confirmation", "Open your account link.")));
        }

        public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
        {
            Acknowledged++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestTransport : IMailTransport
    {
        public bool Enabled { get; init; } = true;

        public bool Validated { get; private set; }

        public bool BlockSend { get; init; }

        public string? FailRecipient { get; init; }

        public List<string> Attempts { get; } = [];

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Validate()
        {
            Validated = true;
        }

        public async Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
        {
            Attempts.Add(message.Recipient);
            Started.TrySetResult();

            if (message.Recipient == FailRecipient)
            {
                throw new IOException("Simulated transport outage.");
            }

            if (BlockSend)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }
}
