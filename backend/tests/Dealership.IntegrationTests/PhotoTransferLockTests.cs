using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PhotoTransferLockTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task ContendedAcquisitionExpiresWithoutCancellingTheHolderAndReleasesWaitingSessions()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.RuntimeConnectionString).Options;
        await using var database = new DealershipDbContext(options);
        var transfers = new PhotoTransferLock(database);
        var id = Guid.NewGuid();
        await using var holder = await transfers.AcquireAsync(id, CancellationToken.None);
        Assert.Null(await transfers.TryAcquireAsync(id, CancellationToken.None));

        // The watchdog bounds a broken implementation; it must not be what cancels the acquisition.
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transfers.AcquireAsync(id, watchdog.Token));
        Assert.False(watchdog.IsCancellationRequested);
        Assert.Null(await transfers.TryAcquireAsync(id, CancellationToken.None));
        await AssertSessionCountAsync(1);

        using var caller = new CancellationTokenSource();
        var cancelled = transfers.AcquireAsync(id, caller.Token);
        await WaitForBlockedSessionAsync();
        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        await AssertSessionCountAsync(1);

        var waiting = transfers.AcquireAsync(id, watchdog.Token);
        await WaitForBlockedSessionAsync();
        await holder.DisposeAsync();
        await using (var acquired = await waiting)
        {
            Assert.Null(await transfers.TryAcquireAsync(id, CancellationToken.None));
        }

        await using (var reacquired = await transfers.TryAcquireAsync(id, CancellationToken.None))
        {
            Assert.NotNull(reacquired);
        }

        await AssertSessionCountAsync(0);
    }

    private async Task WaitForBlockedSessionAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var observer = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_locks
                WHERE locktype = 'advisory' AND NOT granted
                  AND database = (SELECT oid FROM pg_database WHERE datname = current_database()))
            """, observer);

        while (await command.ExecuteScalarAsync(deadline.Token) is not true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
        }
    }

    private async Task AssertSessionCountAsync(long expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var observer = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid()
            """, observer);
        var actual = (long)(await command.ExecuteScalarAsync(deadline.Token))!;

        while (actual != expected && !deadline.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
            actual = (long)(await command.ExecuteScalarAsync(deadline.Token))!;
        }

        Assert.Equal(expected, actual);
    }
}
