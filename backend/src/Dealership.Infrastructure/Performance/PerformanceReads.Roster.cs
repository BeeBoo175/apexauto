using System.Data;
using Dealership.Application.Performance;
using Dealership.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

public sealed partial class PerformanceReads
{
    // When customers are booked, to plan who is needed: cancelled bookings are not demand.
    public async Task<PerformanceReadResult<PerformanceRoster>> RosterAsync(Guid actorId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var scope = await ResolveScopeAsync(actorId, query, cancellationToken);
        if (scope is null)
        {
            return new(PerformanceReadStatus.Forbidden);
        }

        var branchId = scope.BranchId;
        var appointments = await database.IntakeAppointments.AsNoTracking()
            .Where(item => item.CancelledAt == null && item.StartsAt >= query.From && item.StartsAt < query.Until &&
                (branchId == null || item.BranchId == branchId))
            .Select(item => item.StartsAt)
            .ToListAsync(cancellationToken);
        var testDrives = await database.TestDriveBookings.AsNoTracking()
            .Where(item => item.StartsAt >= query.From && item.StartsAt < query.Until &&
                (branchId == null || item.BranchId == branchId) &&
                !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id))
            .Select(item => item.StartsAt)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var cells = new SortedDictionary<(int Weekday, int Hour), (int Appointments, int TestDrives)>();
        foreach (var startsAt in appointments)
        {
            var key = CellOf(startsAt);
            cells[key] = (cells.GetValueOrDefault(key).Appointments + 1, cells.GetValueOrDefault(key).TestDrives);
        }

        foreach (var startsAt in testDrives)
        {
            var key = CellOf(startsAt);
            cells[key] = (cells.GetValueOrDefault(key).Appointments, cells.GetValueOrDefault(key).TestDrives + 1);
        }

        var result = cells.Select(item => new PerformanceRosterCell(item.Key.Weekday, item.Key.Hour,
            item.Value.Appointments, item.Value.TestDrives)).ToArray();
        return new(PerformanceReadStatus.Succeeded, new PerformanceRoster(scope.Actor, result));
    }

    private static (int Weekday, int Hour) CellOf(DateTimeOffset startsAt)
    {
        var local = TimeZoneInfo.ConvertTime(startsAt, DealershipLocale.Malaysia);
        return (((int)local.DayOfWeek + 6) % 7, local.Hour);
    }
}
