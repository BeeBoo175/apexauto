using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

// Manager operations reporting: every section is a bounded aggregate over retained event rows, attributed to the
// branch recorded at the event, never to a person's current branch.
public sealed class PerformanceReads(DealershipDbContext database) : IPerformanceReads
{
    public async Task<PerformanceReadResult<PerformanceHeadline>> HeadlineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var previousFrom = query.From - query.Length;
        var current = await TotalsAsync(scope.BranchId, query.From, query.Until, now, cancellationToken);
        var previous = await TotalsAsync(scope.BranchId, previousFrom, query.From, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PerformanceReadStatus.Succeeded, new PerformanceHeadline(scope.Actor, current, previous));
    }

    public async Task<PerformanceReadResult<PerformancePipeline>> PipelineAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var open = SalesInScope(scope.BranchId).Where(order =>
            order.Status == SalesOrderStatus.Handling || order.Status == SalesOrderStatus.PendingApproval ||
            order.Status == SalesOrderStatus.Approved || order.Status == SalesOrderStatus.Settled ||
            order.Status == SalesOrderStatus.Open && (order.DueAt == null || order.DueAt > now));
        var rows = await open.GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Total = group.Sum(order => order.BalancePayable + order.DepositCredit + order.TradeInAllowance)
            })
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var groups = PerformancePipelineGroups.Ordered.Select(name =>
        {
            var row = rows.SingleOrDefault(item => GroupOf(item.Status) == name);
            return new PerformancePipelineGroup(name, row?.Count ?? 0, row?.Total ?? 0m);
        }).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformancePipeline(scope.Actor, groups));
    }

    private static string GroupOf(SalesOrderStatus status)
    {
        return status switch
        {
            SalesOrderStatus.Open => PerformancePipelineGroups.Unclaimed,
            SalesOrderStatus.Handling => PerformancePipelineGroups.Handling,
            SalesOrderStatus.PendingApproval => PerformancePipelineGroups.NeedsApproval,
            SalesOrderStatus.Approved => PerformancePipelineGroups.Approved,
            _ => PerformancePipelineGroups.SettledUndelivered
        };
    }

    // The branch managers read is their own branch; a GeneralManager reads all branches or one chosen branch.
    private async Task<PerformanceScope?> ResolveScopeAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == actorId && user.IsActive &&
                (user.Role == AccountRoles.BranchManager || user.Role == AccountRoles.GeneralManager))
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return null;
        }

        return actor.Role == AccountRoles.BranchManager
            ? new(actor, actor.BranchId)
            : new(actor, query.BranchId);
    }

    private async Task<PerformancePeriodTotals> TotalsAsync(Guid? branchId, DateTimeOffset periodStart, DateTimeOffset periodEnd,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var delivered = SalesInScope(branchId)
            .Where(order => order.DeliveredAt >= periodStart && order.DeliveredAt < periodEnd);
        var deliveredCount = await delivered.CountAsync(cancellationToken);
        var deliveredValue = await delivered
            .SumAsync(order => (decimal?)(order.BalancePayable + order.DepositCredit + order.TradeInAllowance),
                cancellationToken) ?? 0m;

        var firstListings = database.ListingPublications.AsNoTracking()
            .Where(publication => branchId == null || publication.BranchId == branchId)
            .GroupBy(publication => publication.VehicleId)
            .Select(group => new { VehicleId = group.Key, ListedAt = group.Min(publication => publication.RecordedAt) });
        var listed = from listing in firstListings
                     join checkIn in database.AppointmentCheckIns.AsNoTracking() on listing.VehicleId equals checkIn.VehicleId
                     where listing.ListedAt >= periodStart && listing.ListedAt < periodEnd
                     select (listing.ListedAt - checkIn.CheckedInAt).TotalSeconds;
        var listedCount = await listed.CountAsync(cancellationToken);
        var averageSeconds = listedCount == 0 ? (double?)null : await listed.AverageAsync(cancellationToken);

        // A no-show is a booked day that has ended with no cancellation and no check-in, corrected ones included.
        var todayStartsAt = AppointmentDay.StartUtc(now);
        var noShows = await database.IntakeAppointments.AsNoTracking()
            .Where(appointment => (branchId == null || appointment.BranchId == branchId) &&
                appointment.CancelledAt == null && appointment.StartsAt >= periodStart && appointment.StartsAt < periodEnd &&
                appointment.StartsAt < todayStartsAt &&
                !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == appointment.Id))
            .CountAsync(cancellationToken);
        return new(deliveredCount, deliveredValue, listedCount, averageSeconds / TimeSpan.SecondsPerDay, noShows);
    }

    private IQueryable<SalesOrder> SalesInScope(Guid? branchId)
    {
        return database.SalesOrders.AsNoTracking().Where(order => branchId == null || order.BranchId == branchId);
    }

    private sealed record PerformanceScope(CurrentActor Actor, Guid? BranchId);
}
