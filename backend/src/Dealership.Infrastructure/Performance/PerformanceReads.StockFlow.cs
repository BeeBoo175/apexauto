using System.Data;
using Dealership.Application.Performance;
using Dealership.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // Weekly vehicles in and out. A no-show attendance correction is a manager's record, not a vehicle arriving.
    public async Task<PerformanceReadResult<PerformanceStockFlow>> StockFlowAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var checkIns = await database.AppointmentCheckIns.AsNoTracking()
            .Where(item => item.CorrectionRecordedAt == null && item.CheckedInAt >= query.From &&
                item.CheckedInAt < query.Until && (scope.BranchId == null || item.BranchId == scope.BranchId))
            .Select(item => item.CheckedInAt)
            .ToListAsync(cancellationToken);
        var deliveries = await SalesInScope(scope.BranchId)
            .Where(order => order.DeliveredAt >= query.From && order.DeliveredAt < query.Until)
            .Select(order => order.DeliveredAt!.Value)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var weeks = new SortedDictionary<DateOnly, (int CheckIns, int Deliveries)>();
        for (var week = WeekStart(query.From); week < MalaysiaDate(query.Until); week = week.AddDays(7))
        {
            weeks[week] = (0, 0);
        }

        foreach (var at in checkIns)
        {
            var week = WeekStart(at);
            weeks[week] = (weeks[week].CheckIns + 1, weeks[week].Deliveries);
        }

        foreach (var at in deliveries)
        {
            var week = WeekStart(at);
            weeks[week] = (weeks[week].CheckIns, weeks[week].Deliveries + 1);
        }

        var result = weeks.Select(item => new PerformanceStockWeek(item.Key, item.Value.CheckIns, item.Value.Deliveries))
            .ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceStockFlow(scope.Actor, result));
    }

    private static DateOnly MalaysiaDate(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, DealershipLocale.Malaysia).DateTime);
    }

    private static DateOnly WeekStart(DateTimeOffset instant)
    {
        var date = MalaysiaDate(instant);
        var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-sinceMonday);
    }
}
