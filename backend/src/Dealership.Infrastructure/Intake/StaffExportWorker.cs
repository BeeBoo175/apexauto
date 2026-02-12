using System.Threading.Channels;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dealership.Infrastructure.Intake;

public sealed class StaffExportWorker(IServiceScopeFactory scopes) : BackgroundService, IStaffExportWorker
{
    private readonly SemaphoreSlim _admission = new(1, 1);
    private readonly Channel<Work> _requests = Channel.CreateBounded<Work>(1);

    public async Task<Stream?> GenerateAsync(CurrentActor actor, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        if (!_admission.Wait(0, cancellationToken))
        {
            throw new StaffExportBusyException();
        }

        try
        {
            var work = new Work(actor, query, cancellationToken);
            if (!_requests.Writer.TryWrite(work))
            {
                throw new OperationCanceledException("The export worker is stopping.", cancellationToken);
            }

            return await work.Completion.Task;
        }
        finally
        {
            _admission.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _requests.Reader.ReadAllAsync(stoppingToken))
            {
                await GenerateNextAsync(work, stoppingToken);
            }
        }
        finally
        {
            _requests.Writer.TryComplete();
            while (_requests.Reader.TryRead(out var pending))
            {
                pending.Completion.TrySetCanceled(stoppingToken);
            }
        }
    }

    private async Task GenerateNextAsync(Work work, CancellationToken stoppingToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, work.Cancellation);
        deadline.CancelAfter(StaffExportLimits.BackgroundDeadline);

        try
        {
            work.Completion.SetResult(await CreateFileAsync(work, deadline.Token));
        }
        catch (OperationCanceledException)
        {
            work.Completion.TrySetCanceled(deadline.Token);
        }
        catch (Exception exception)
        {
            work.Completion.TrySetException(exception);
        }
    }

    private async Task<Stream?> CreateFileAsync(Work work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopes.CreateAsyncScope();
        var file = new TemporaryExportFile();
        var handedOff = false;
        try
        {
            var allowed = await scope.ServiceProvider.GetRequiredService<IStaffExportWriter>()
                .WriteAsync(work.Actor, work.Query, file, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!allowed)
            {
                return null;
            }

            file.Position = 0;
            handedOff = true;
            return file;
        }
        finally
        {
            if (!handedOff)
            {
                await file.DisposeAsync();
            }
        }
    }

    private sealed record Work(CurrentActor Actor, StaffWorklistQuery Query, CancellationToken Cancellation)
    {
        public TaskCompletionSource<Stream?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
