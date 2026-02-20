using System.Text;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.UnitTests;

public sealed class StaffExportWorkerTests
{
    private static readonly CurrentActor Actor = new(Guid.NewGuid(), AccountRoles.BranchManager, Guid.NewGuid());
    private static readonly StaffWorklistQuery Query = new(null, null, StaffWorklistStatus.All, null, 1, 25);

    [Fact]
    public async Task BusyWorkerRejectsOverlapAndCancellationReleasesTheSlotAndTemporaryFile()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new Writer(async (_, _, _, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        using var services = Services(writer);
        using var worker = new StaffExportWorker(services.GetRequiredService<IServiceScopeFactory>());
        await worker.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var running = worker.GenerateAsync(Actor, Query, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<StaffExportBusyException>(() => worker.GenerateAsync(Actor, Query, CancellationToken.None));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.False(File.Exists(writer.Path));

        writer.Write = async (actor, query, destination, token) =>
        {
            Assert.Equal(Actor, actor);
            Assert.Equal(Query, query);
            await using var text = new StreamWriter(destination, new UTF8Encoding(false), 4096, leaveOpen: true);
            await text.WriteLineAsync("reference".AsMemory(), token);
            for (var index = 0; index < 10001; index++)
            {
                await text.WriteLineAsync(index.ToString(System.Globalization.CultureInfo.InvariantCulture).AsMemory(), token);
            }
            return true;
        };
        await using (var result = await worker.GenerateAsync(Actor, Query, CancellationToken.None))
        {
            Assert.NotNull(result);
            using var reader = new StreamReader(result, leaveOpen: true);
            Assert.Equal(10003, (await reader.ReadToEndAsync()).Split('\n').Length);
        }
        Assert.False(File.Exists(writer.Path));
        await worker.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("forbidden")]
    [InlineData("oversized")]
    public async Task FailedAndDeniedWorkLeavesNoFileAndDoesNotBlockLaterExports(string outcome)
    {
        var writer = new Writer(async (_, _, destination, token) =>
        {
            if (outcome == "failure")
            {
                throw new IOException("Test storage failure");
            }
            if (outcome == "forbidden")
            {
                return false;
            }
            destination.Position = StaffExportLimits.BackgroundBytes;
            await destination.WriteAsync(new byte[1], token);
            return true;
        });
        using var services = Services(writer);
        using var worker = new StaffExportWorker(services.GetRequiredService<IServiceScopeFactory>());
        await worker.StartAsync(CancellationToken.None);
        var attempt = worker.GenerateAsync(Actor, Query, CancellationToken.None);
        if (outcome == "forbidden")
        {
            Assert.Null(await attempt);
        }
        else
        {
            await Assert.ThrowsAnyAsync<IOException>(() => attempt);
        }
        Assert.False(File.Exists(writer.Path));
        writer.Write = (_, _, _, _) => Task.FromResult(true);
        await using (var retry = await worker.GenerateAsync(Actor, Query, CancellationToken.None))
        {
            Assert.NotNull(retry);
        }
        Assert.False(File.Exists(writer.Path));
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StoppingTheHostCancelsActiveWorkAndRejectsNewRequests()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new Writer(async (_, _, _, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        });
        using var services = Services(writer);
        using var worker = new StaffExportWorker(services.GetRequiredService<IServiceScopeFactory>());
        await worker.StartAsync(CancellationToken.None);
        var running = worker.GenerateAsync(Actor, Query, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.False(File.Exists(writer.Path));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.GenerateAsync(Actor, Query, CancellationToken.None));
    }

    private static ServiceProvider Services(Writer writer)
    {
        return new ServiceCollection().AddSingleton<IStaffExportWriter>(writer).BuildServiceProvider();
    }

    private sealed class Writer(Func<CurrentActor, StaffWorklistQuery, Stream, CancellationToken, Task<bool>> write)
        : IStaffExportWriter
    {
        public Func<CurrentActor, StaffWorklistQuery, Stream, CancellationToken, Task<bool>> Write { get; set; } = write;
        public string? Path { get; private set; }

        public Task<bool> WriteAsync(CurrentActor actor, StaffWorklistQuery query, Stream destination, CancellationToken cancellationToken)
        {
            Path = ((FileStream)destination).Name;
            return Write(actor, query, destination, cancellationToken);
        }
    }
}
