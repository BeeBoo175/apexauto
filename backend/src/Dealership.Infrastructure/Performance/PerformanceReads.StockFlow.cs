using System.Data;
using Dealership.Application.Performance;
using Dealership.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // Weekly vehicles in and out, with the agreed value delivered each week (the same agreed total as the headline).
    // A no-show attendance correction is a manager's record, not a vehicle arriving.
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

        var result = await WeeklyFlowAsync(scope.BranchId, query, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PerformanceReadStatus.Succeeded, new PerformanceStockFlow(scope.Actor, result));
    }

    // The weekly flow of one branch, or of every branch in scope when none is chosen.
    private async Task<PerformanceStockWeek[]> WeeklyFlowAsync(Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var checkIns = await database.AppointmentCheckIns.AsNoTracking()
            .Where(item => item.CorrectionRecordedAt == null && item.CheckedInAt >= query.From &&
                item.CheckedInAt < query.Until && (branchId == null || item.BranchId == branchId))
            .Select(item => item.CheckedInAt)
            .ToListAsync(cancellationToken);
        var deliveries = await SalesInScope(branchId)
            .Where(order => order.DeliveredAt >= query.From && order.DeliveredAt < query.Until)
            .Select(order => new
            {
                At = order.DeliveredAt!.Value,
                Value = order.BalancePayable + order.DepositCredit + order.TradeInAllowance,
            })
            .ToListAsync(cancellationToken);

        var weeks = new SortedDictionary<DateOnly, (int CheckIns, int Deliveries, decimal Value)>();
        for (var week = WeekStart(query.From); week < MalaysiaDate(query.Until); week = week.AddDays(7))
        {
            weeks[week] = (0, 0, 0m);
        }

        foreach (var at in checkIns)
        {
            var week = WeekStart(at);
            weeks[week] = weeks[week] with { CheckIns = weeks[week].CheckIns + 1 };
        }

        foreach (var delivery in deliveries)
        {
            var week = WeekStart(delivery.At);
            weeks[week] = weeks[week] with
            {
                Deliveries = weeks[week].Deliveries + 1,
                Value = weeks[week].Value + delivery.Value,
            };
        }

        return [.. weeks.Select(item =>
            new PerformanceStockWeek(item.Key, item.Value.CheckIns, item.Value.Deliveries, item.Value.Value))];
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
