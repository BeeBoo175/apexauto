using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Domain.Sales;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    private static readonly string[] SalesTeamRoles = [AccountRoles.SalesAgent, AccountRoles.BranchManager];

    // Sales & service: every agent in scope appears; a manager only when they handled Intake or test drives.
    public async Task<PerformanceReadResult<PerformanceTeamPage<PerformanceSalesRow>>> SalesTeamAsync(Guid actorId,
        PerformanceTeamQuery query, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query.Period, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var period = query.Period;
        var branchId = scope.BranchId;
        var people = await TeamPeople.ReadAsync(database, SalesTeamRoles, cancellationToken);
        var checkIns = await database.AppointmentCheckIns.AsNoTracking()
            .Where(item => item.CheckedInAt >= period.From && item.CheckedInAt < period.Until &&
                (branchId == null || item.BranchId == branchId))
            .GroupBy(item => new { item.StaffId, item.BranchId, Corrected = item.CorrectionRecordedAt != null })
            .Select(group => new { group.Key.StaffId, group.Key.BranchId, group.Key.Corrected, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var drives = await database.TestDriveOutcomes.AsNoTracking()
            .Where(item => item.EndedAt >= period.From && item.EndedAt < period.Until &&
                (branchId == null || item.BranchId == branchId))
            .GroupBy(item => new { item.StaffId, item.BranchId, item.Kind })
            .Select(group => new { group.Key.StaffId, group.Key.BranchId, group.Key.Kind, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var sales = await (from entry in database.SalesOrderEvents.AsNoTracking()
                           join order in database.SalesOrders.AsNoTracking() on entry.SalesOrderId equals order.Id
                           where (entry.Kind == SalesOrderEventKind.Settled || entry.Kind == SalesOrderEventKind.Delivered) &&
                               entry.RecordedAt >= period.From && entry.RecordedAt < period.Until &&
                               (branchId == null || order.BranchId == branchId)
                           group entry by new { PersonId = entry.ActorId!.Value, order.BranchId, entry.Kind } into grouped
                           select new { grouped.Key.PersonId, grouped.Key.BranchId, grouped.Key.Kind, Count = grouped.Count() })
            .ToListAsync(cancellationToken);
        var deliveryIntervals = await DeliveryIntervalsAsync(period, branchId, cancellationToken);
        var intake = await IntakeWorkAsync(branchId, await database.DatabaseNowAsync(cancellationToken), cancellationToken);
        var driving = await TestDriveWorkAsync(branchId, cancellationToken);
        var handled = await SaleWorkAsync(branchId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var keys = checkIns.Select(item => (item.StaffId, item.BranchId))
            .Concat(drives.Select(item => (item.StaffId, item.BranchId)))
            .Concat(sales.Select(item => (item.PersonId, item.BranchId)))
            .Where(key => people.Includes(key.Item1))
            .Concat(people.Current(AccountRoles.SalesAgent, branchId))
            .Distinct()
            .ToArray();
        var rows = keys.Select(key =>
        {
            var (personId, rowBranch) = key;
            var current = people.IsCurrent(personId, rowBranch);
            return new PerformanceSalesRow(personId, people.Name(personId), people.Role(personId), rowBranch,
                people.BranchName(rowBranch), people.Picture(personId), people.Status(personId, rowBranch),
                checkIns.Where(item => item.StaffId == personId && item.BranchId == rowBranch && !item.Corrected)
                    .Sum(item => item.Count),
                checkIns.Where(item => item.StaffId == personId && item.BranchId == rowBranch && item.Corrected)
                    .Sum(item => item.Count),
                drives.Where(item => item.StaffId == personId && item.BranchId == rowBranch &&
                    item.Kind == TestDriveOutcomeKind.Completed).Sum(item => item.Count),
                drives.Where(item => item.StaffId == personId && item.BranchId == rowBranch &&
                    item.Kind == TestDriveOutcomeKind.NotCompleted).Sum(item => item.Count),
                sales.Where(item => item.PersonId == personId && item.BranchId == rowBranch &&
                    item.Kind == SalesOrderEventKind.Settled).Sum(item => item.Count),
                sales.Where(item => item.PersonId == personId && item.BranchId == rowBranch &&
                    item.Kind == SalesOrderEventKind.Delivered).Sum(item => item.Count),
                AverageDays(deliveryIntervals.Where(item => item.PersonId == personId && item.BranchId == rowBranch)
                    .Select(item => item.Seconds)),
                current ? intake.Count(item => item.Item1 == personId) : null,
                current ? driving.Count(item => item.Item1 == personId) : null,
                current ? handled.Count(item => item.Item1 == personId) : null);
        });
        return new(PerformanceReadStatus.Succeeded, PageOf(scope, query, rows, SalesColumns,
            row => new PerformanceTeamPoint(row.PersonId, row.BranchId, row.Name, row.SalesDelivered,
                row.AssignmentToDeliveryDays)));
    }

    private static readonly TeamColumns<PerformanceSalesRow> SalesColumns = new(row => row.PersonId,
        row => row.Name, row => row.BranchName, row => row.Status,
        new Dictionary<string, Func<PerformanceSalesRow, double?>>
        {
            ["checkIns"] = row => row.CheckIns,
            ["drivesCompleted"] = row => row.DrivesCompleted,
            ["drivesNotCompleted"] = row => row.DrivesNotCompleted,
            ["salesSettled"] = row => row.SalesSettled,
            ["salesDelivered"] = row => row.SalesDelivered,
            ["noShowCorrections"] = row => row.NoShowCorrections,
            ["assignmentToDelivery"] = row => row.AssignmentToDeliveryDays,
            ["intakeHandled"] = row => row.IntakeHandled,
            ["testDrivesHeld"] = row => row.TestDrivesHeld,
            ["salesHandled"] = row => row.SalesHandled
        });

    // From the delivering agent's latest claim, assignment or reassignment of the sale to its delivery.
    private async Task<List<(Guid PersonId, Guid BranchId, double Seconds)>> DeliveryIntervalsAsync(PerformanceQuery query,
        Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = await (from delivered in database.SalesOrderEvents.AsNoTracking()
                          join order in database.SalesOrders.AsNoTracking() on delivered.SalesOrderId equals order.Id
                          where delivered.Kind == SalesOrderEventKind.Delivered && delivered.RecordedAt >= query.From &&
                              delivered.RecordedAt < query.Until && (branchId == null || order.BranchId == branchId)
                          select new
                          {
                              PersonId = delivered.ActorId!.Value,
                              order.BranchId,
                              Delivered = delivered.RecordedAt,
                              Assigned = database.SalesOrderEvents
                                  .Where(item => item.SalesOrderId == delivered.SalesOrderId &&
                                      item.Sequence < delivered.Sequence &&
                                      (item.Kind == SalesOrderEventKind.Claimed || item.Kind == SalesOrderEventKind.Assigned ||
                                          item.Kind == SalesOrderEventKind.Reassigned))
                                  .OrderByDescending(item => item.Sequence)
                                  .Select(item => (DateTimeOffset?)item.RecordedAt).FirstOrDefault()
                          }).ToListAsync(cancellationToken);
        return rows.Where(row => row.Assigned != null)
            .Select(row => (row.PersonId, row.BranchId, (row.Delivered - row.Assigned!.Value).TotalSeconds)).ToList();
    }
}
