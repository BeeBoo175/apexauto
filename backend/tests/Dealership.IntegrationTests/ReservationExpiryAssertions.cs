using System.Collections.Concurrent;
using System.Data.Common;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class ReservationExpiryAssertions
{
    internal static async Task RunAsync(ReservationTestClock clock, IServiceProvider services, Guid owner,
        IReadOnlyList<ListingRecord> listings)
    {
        var customers = new List<Guid> { owner };
        var holds = new List<Reservation>();
        var signal = new ReservationExpirySignal();
        for (var index = 0; index < listings.Count; index++)
        {
            if (index > 0)
            {
                customers.Add(await RegisterCustomerAsync(services, clock.Maintenance));
            }

            holds.Add(await HoldAsync(clock, customers[index], listings[index].Publication, signal));
        }

        // Many committed confirmations coalesce into one execution hint.
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await AssertNoSignalAsync(signal);
        var ids = holds.Select(item => item.Id).ToArray();
        var probe = new ExecutionProbe();
        await using var provider = CreateProvider(clock, probe);
        using var worker = CreateWorker(provider, signal);
        await clock.AdvanceAsync(holds[0].ExpiresAt.AddTicks(-10));
        await worker.RunBatchAsync(CancellationToken.None);
        Assert.False(await clock.Maintenance.ReservationReleases.AnyAsync(item => ids.Contains(item.ReservationId)));
        Assert.Empty(probe.Vehicles);
        await AssertNoSignalAsync(signal);

        await clock.AdvanceAsync(holds[0].ExpiresAt);
        var ordered = await clock.Maintenance.Reservations.Where(item => ids.Contains(item.Id))
            .OrderBy(item => item.VehicleId).Select(item => item.VehicleId).ToArrayAsync();
        await worker.RunBatchAsync(CancellationToken.None);
        Assert.Equal(ordered.Take(10), probe.Vehicles);
        Assert.Equal(10, await clock.Maintenance.ReservationReleases.CountAsync(item => ids.Contains(item.ReservationId)));
        // Consume the continuation hint explicitly to prove a full batch schedules more work.
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await worker.RunBatchAsync(CancellationToken.None);
        await AssertRefundsAsync(clock, holds);
        await AssertNoSignalAsync(signal);

        holds.Clear();
        for (var index = 0; index < listings.Count; index++)
        {
            holds.Add(await HoldAsync(clock, customers[index], listings[index].Publication, new ReservationExpirySignal()));
        }

        await clock.AdvanceAsync(holds[0].ExpiresAt);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            // Startup must keep draining a backlog larger than one batch without a business event.
            await WaitUntilAsync(async () => !await clock.Maintenance.Reservations.AnyAsync(item => item.IsActive));
            await AssertRefundsAsync(clock, holds);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await AssertEventsAndRestartAsync(clock, provider, probe, signal, customers, listings);
        await AssertFailureRecoveryAsync(clock, customers[0], listings[0].Publication);
    }

    private static async Task AssertEventsAndRestartAsync(ReservationTestClock clock, ServiceProvider provider,
        ExecutionProbe probe, ReservationExpirySignal signal, List<Guid> customers,
        IReadOnlyList<ListingRecord> listings)
    {
        var held = await HoldAsync(clock, customers[0], listings[0].Publication, signal);
        using (var worker = CreateWorker(provider, signal))
        {
            var beforeStartup = probe.Commits;
            await worker.StartAsync(CancellationToken.None);
            try
            {
                await WaitUntilAsync(() => Task.FromResult(probe.Commits > beforeStartup));
                var reads = probe.Reads;
                await clock.AdvanceAsync(held.ExpiresAt);
                await Task.Delay(150);
                Assert.Equal(reads, probe.Reads);
                Assert.False(await clock.Maintenance.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));

                // Another customer's ordinary commit wakes global due work without touching the expired vehicle.
                var activity = await HoldAsync(clock, customers[1], listings[1].Publication, signal);
                await WaitUntilAsync(() => clock.Maintenance.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));
                await AssertRefundsAsync(clock, [held]);
                Assert.True(await clock.Maintenance.Reservations.Where(item => item.Id == activity.Id)
                    .Select(item => item.IsActive).SingleAsync());
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }
        }

        // Stop before the next deadline, then resume solely from persisted work at startup.
        var restartedHold = await HoldAsync(clock, customers[0], listings[0].Publication, new ReservationExpirySignal());
        await clock.AdvanceAsync(restartedHold.ExpiresAt);
        var persisted = await clock.Maintenance.ReservationReleases.Select(item => item.Id).ToArrayAsync();
        using var restarted = CreateWorker(provider, new ReservationExpirySignal());
        await restarted.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () => !await clock.Maintenance.Reservations.AnyAsync(item => item.IsActive));
            await AssertRefundsAsync(clock, [restartedHold]);
            Assert.Equal(persisted.Length, await clock.Maintenance.ReservationReleases.CountAsync(item => persisted.Contains(item.Id)));
        }
        finally
        {
            await restarted.StopAsync(CancellationToken.None);
        }
    }

    private static async Task AssertFailureRecoveryAsync(ReservationTestClock clock, Guid customer,
        ListingPublication publication)
    {
        var held = await HoldAsync(clock, customer, publication, new ReservationExpirySignal());
        await clock.AdvanceAsync(held.ExpiresAt);
        var signal = new ReservationExpirySignal();
        var probe = new ExecutionProbe { Fail = true };
        await using var provider = CreateProvider(clock, probe);
        using var worker = CreateWorker(provider, signal);
        await worker.RunBatchAsync(CancellationToken.None);
        Assert.Equal(3, probe.Vehicles.Count);
        Assert.Equal(0, probe.Commits);
        Assert.False(await clock.Maintenance.ReservationReleases.AnyAsync(item => item.ReservationId == held.Id));
        await AssertNoSignalAsync(signal);
        probe.Fail = false;
        await Task.WhenAll(worker.RunBatchAsync(CancellationToken.None), RunCompetingAsync(clock));
        await AssertRefundsAsync(clock, [held]);

        held = await HoldAsync(clock, customer, publication, new ReservationExpirySignal());
        await clock.AdvanceAsync(held.ExpiresAt);
        var attempts = probe.Vehicles.Count;
        var reads = probe.Reads;
        var commits = probe.Commits;
        probe.BeforeLock = async () =>
        {
            await using var writer = clock.CreateContext();
            var result = await new ReservationCancellationService(new ReservationCancellationStore(writer))
                .CancelAsync(customer, held.Id, new(Guid.NewGuid(), null), CancellationToken.None);
            Assert.Equal(ReservationStatus.Conflict, result.Status);
        };
        await worker.RunBatchAsync(CancellationToken.None);
        Assert.Equal(attempts + 1, probe.Vehicles.Count);
        Assert.True(probe.Reads >= reads + 5, "A competing release must retry the entire snapshot, including the database clock.");
        Assert.Equal(commits + 1, probe.Commits);
        Assert.Null(probe.BeforeLock);
        await AssertRefundsAsync(clock, [held]);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RunBatchAsync(cancelled.Token));
    }

    private static async Task RunCompetingAsync(ReservationTestClock clock)
    {
        await using var provider = CreateProvider(clock, new ExecutionProbe());
        using var worker = CreateWorker(provider, new ReservationExpirySignal());
        await worker.RunBatchAsync(CancellationToken.None);
    }

    private static ServiceProvider CreateProvider(ReservationTestClock clock, ExecutionProbe probe)
    {
        using var source = clock.CreateContext();
        var connection = source.Database.GetConnectionString();
        var services = new ServiceCollection();
        services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(connection)
            .AddInterceptors(probe, new BatchCompletion(probe)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static ReservationExpiryWorker CreateWorker(ServiceProvider provider, ReservationExpirySignal signal)
    {
        return new(provider.GetRequiredService<IServiceScopeFactory>(), signal, NullLogger<ReservationExpiryWorker>.Instance);
    }

    private static async Task<Reservation> HoldAsync(ReservationTestClock clock, Guid customer,
        ListingPublication publication, ReservationExpirySignal signal)
    {
        await using var writer = clock.CreateContext();
        var result = await new ReservationService(new ReservationStore(writer, new MailDeliverySignal(), signal))
            .ReserveAsync(customer, publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private static async Task AssertRefundsAsync(ReservationTestClock clock, IReadOnlyList<Reservation> holds)
    {
        foreach (var held in holds)
        {
            var refund = Assert.Single(await clock.Maintenance.ReservationReleases.AsNoTracking()
                .Where(item => item.ReservationId == held.Id).ToArrayAsync());
            Assert.Equal(ReservationReleaseKind.Expired, refund.Kind);
            Assert.Equal(500m, refund.Refund);
            Assert.Equal(held.ExpiresAt, refund.RecordedAt);
            Assert.False(await clock.Maintenance.Reservations.Where(item => item.Id == held.Id)
                .Select(item => item.IsActive).SingleAsync());
        }
    }

    private static async Task AssertNoSignalAsync(ReservationExpirySignal signal)
    {
        using var cancellation = new CancellationTokenSource();
        var waiting = signal.WaitAsync(cancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> completed)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await completed())
        {
            await Task.Delay(25, deadline.Token);
        }
    }

    private sealed class ExecutionProbe : DbCommandInterceptor
    {
        private int _reads;
        private int _commits;
        public int Reads => Volatile.Read(ref _reads);
        public int Commits => Volatile.Read(ref _commits);
        public ConcurrentQueue<Guid> Vehicles { get; } = new();
        public bool Fail { get; set; }
        public Func<Task>? BeforeLock { get; set; }

        public void Committed()
        {
            Interlocked.Increment(ref _commits);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reads);
            return ValueTask.FromResult(result);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM \"Vehicles\"", StringComparison.Ordinal))
            {
                return result;
            }

            Vehicles.Enqueue((Guid)command.Parameters[0].Value!);
            if (BeforeLock is not null)
            {
                var callback = BeforeLock;
                BeforeLock = null;
                await callback();
            }

            if (!Fail)
            {
                return result;
            }

            await using var failure = command.Connection!.CreateCommand();
            failure.Transaction = command.Transaction;
            failure.CommandText = "DO $$ BEGIN RAISE EXCEPTION 'Concurrent reservation change' USING ERRCODE = '40001'; END $$";
            await failure.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }

    private sealed class BatchCompletion(ExecutionProbe probe) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            probe.Committed();
            return Task.CompletedTask;
        }
    }
}
