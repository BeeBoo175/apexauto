using Dealership.Domain.Common;
using Dealership.Application.Common;
using Dealership.Application.Intake;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

internal static class WorkCalendarScheduledRows
{
    public static async Task<WorkCalendarEvent[]?> ReadAsync(DealershipDbContext database,
        WorkCalendarReadScope scope, WorkCalendarQuery query, CancellationToken cancellationToken)
    {
        var intakeQuery = new StaffWorklistQuery(query.Dates.From, query.Dates.Until,
            StaffWorklistStatus.All, scope.BranchId, 1, Pagination.DefaultPageSize);
        var intake = (await StaffWorklistRows.CreateAsync(database, scope.Actor.Id, intakeQuery, cancellationToken))!;
        var now = await database.DatabaseNowAsync(cancellationToken);
        var drives = StaffTestDriveRows.Create(database, scope.BranchId, AppointmentDay.StartUtc(now))
            .Where(row => row.StartsAt >= query.Dates.From && row.StartsAt < query.Dates.Until);
        if (!scope.IsManager)
        {
            intake = intake.Where(row => row.HandlerId == scope.Actor.Id);
            drives = drives.Where(row => row.HandlerId == scope.Actor.Id);
        }
        if (scope.Employee == "unassigned")
        {
            intake = intake.Where(row => row.HandlerId == null);
            drives = drives.Where(row => row.HandlerId == null);
        }
        else if (scope.Employee.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(scope.Employee);
            intake = intake.Where(row => row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!"));
            drives = drives.Where(row => row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!"));
        }

        if (query.Assignment == "Assigned")
        {
            intake = intake.Where(row => row.HandlerId != null);
            drives = drives.Where(row => row.HandlerId != null);
        }
        if (query.Assignment == "Unassigned")
        {
            intake = intake.Where(row => row.HandlerId == null);
            drives = drives.Where(row => row.HandlerId == null);
        }
        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            intake = intake.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                EF.Functions.ILike(row.CustomerName, pattern, "!") ||
                row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!"));
            drives = drives.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                EF.Functions.ILike(row.CustomerName, pattern, "!") ||
                row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!"));
        }

        var total = await intake.CountAsync(cancellationToken) + await drives.CountAsync(cancellationToken);
        if (total > WorkCalendarQuery.MaximumEvents)
        {
            return null;
        }

        var appointments = await intake.ToArrayAsync(cancellationToken);
        var bookings = await drives.ToArrayAsync(cancellationToken);
        var items = appointments.Select(StaffWorklistRows.ToItem).Select(row => new WorkCalendarEvent(
            row.Id, "Intake", row.BranchId, row.BranchName, row.RegistrationPlate,
            $"{row.ManufactureYear} {row.Make} {row.Model} {row.Trim}", row.Status,
            row.HandlerId, row.HandlerEmail, row.StartsAt, row.EndsAt));
        var testDrives = bookings.Select(row => new WorkCalendarEvent(row.Id, "TestDrive", row.BranchId,
            row.BranchName, row.RegistrationPlate, $"{row.ManufactureYear} {row.Make} {row.Model} {row.Trim}",
            row.Status, row.HandlerId, row.HandlerEmail, row.StartsAt, row.EndsAt));
        return items.Concat(testDrives).OrderBy(row => row.StartsAt).ThenBy(row => row.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.Id).ToArray();
    }
}
