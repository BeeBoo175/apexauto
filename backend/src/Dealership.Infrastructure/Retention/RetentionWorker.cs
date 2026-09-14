using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Retention;

// Routine minimization of expired security artifacts, terminal invitations and delivered mail details. Like the
// reservation expiry worker it runs once at start and then only when a commit signals it: no timer, no polling.
public sealed class RetentionWorker(IServiceScopeFactory scopes, IOptions<RetentionOptions> options,
    RetentionSignal signal, PhotoCleanupSignal photoCleanup, ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, string, Exception?> RowFailure = LoggerMessage.Define<string, string>(
        LogLevel.Warning, new EventId(4, nameof(RowFailure)),
        "Retention of {Class} remains retryable ({FailureType}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        signal.Request();
        while (!stoppingToken.IsCancellationRequested)
        {
            // Deadlines are enforced by the next run; an idle host waits for activity or a restart.
            await signal.WaitAsync(stoppingToken);
            await RunBatchAsync(stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        var batchSize = options.Value.BatchSize;
        RetentionCutoffs cutoffs;

        try
        {
            cutoffs = await ReadCutoffsAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            RowFailure(logger, "Settings", exception.GetType().Name, null);
            return;
        }

        var moreWork = false;
        foreach (var rule in RetentionRules.All)
        {
            moreWork |= await RunRuleAsync(rule, cutoffs.For(rule.Horizon), batchSize, cancellationToken);
        }

        if (moreWork)
        {
            // A full class batch committed; drain the rest without waiting for another event.
            signal.Request();
        }
    }

    // One cutoff per run from the database clock, so application and database clocks never mix.
    private async Task<RetentionCutoffs> ReadCutoffsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT now() AS \"Value\"")
            .SingleAsync(cancellationToken);
        // A missing record never blocks minimization: the approved defaults apply until provisioning runs.
        var settings = await database.RetentionSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return new RetentionCutoffs(
            now.AddDays(-(settings?.SecurityArtifactDays ?? RetentionSettings.DefaultDays)),
            now.AddDays(-(settings?.MailDetailDays ?? RetentionSettings.DefaultDays)),
            now.AddDays(-(settings?.InvitationDays ?? RetentionSettings.DefaultDays)),
            now);
    }

    // Walks the class in key order so a failing row is skipped rather than occupying the batch. Returns whether the
    // batch was filled, which means more eligible rows probably remain.
    private async Task<bool> RunRuleAsync(RetentionRule rule, DateTimeOffset cutoff, int batchSize,
        CancellationToken cancellationToken)
    {
        var minimized = 0;
        var after = Guid.Empty;

        while (minimized < batchSize)
        {
            IReadOnlyList<Guid> page;
            try
            {
                page = await ReadPageAsync(rule, cutoff, after, batchSize, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                RowFailure(logger, rule.Name, exception.GetType().Name, null);
                return false;
            }

            foreach (var id in page)
            {
                after = id;
                if (await MinimizeRowAsync(rule, cutoff, id, cancellationToken))
                {
                    minimized++;
                    if (rule.Statement is not null)
                    {
                        // A queued photograph removal waits for the photo worker, which this commit wakes.
                        photoCleanup.Request();
                    }
                }

                if (minimized >= batchSize)
                {
                    return true;
                }
            }

            if (page.Count < batchSize)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<Guid>> ReadPageAsync(RetentionRule rule, DateTimeOffset cutoff, Guid after,
        int limit, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var sql = $"SELECT \"{rule.Key}\" AS \"Value\" FROM \"{rule.Table}\" WHERE ({rule.Eligible}) AND \"{rule.Key}\" > {{1}} " +
            $"ORDER BY \"{rule.Key}\" LIMIT {{2}}";
        return await database.Database.SqlQueryRaw<Guid>(sql, cutoff, after, limit).ToListAsync(cancellationToken);
    }

    private async Task<bool> MinimizeRowAsync(RetentionRule rule, DateTimeOffset cutoff, Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var sql = rule.Statement ??
                $"UPDATE \"{rule.Table}\" SET {rule.Minimize} WHERE \"{rule.Key}\" = {{1}} AND ({rule.Eligible})";
            // A single statement is its own short transaction; the predicate is re-checked under the row lock.
            return await database.Database.ExecuteSqlRawAsync(sql, [cutoff, id], cancellationToken) == 1;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            RowFailure(logger, rule.Name, exception.GetType().Name, null);
            return false;
        }
    }

    private sealed record RetentionCutoffs(DateTimeOffset Security, DateTimeOffset Mail, DateTimeOffset Invitation,
        DateTimeOffset Business)
    {
        public DateTimeOffset For(RetentionHorizon horizon) => horizon switch
        {
            RetentionHorizon.SecurityArtifacts => Security,
            RetentionHorizon.MailDetails => Mail,
            RetentionHorizon.BusinessEvidence => Business,
            _ => Invitation
        };
    }
}
