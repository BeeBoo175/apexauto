using System.Linq.Expressions;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

internal static class StaffWorklistRows
{
    public static async Task<IQueryable<WorklistRow>?> CreateAsync(DealershipDbContext database,
        Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        var scope = await StaffIntakeReadScope.ResolveAsync(database, staffId, cancellationToken);

        if (scope is null ||
            (scope.BranchId is not null && query.BranchId is not null && query.BranchId != scope.BranchId))
        {
            return null;
        }

        var databaseNow = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var today = AppointmentDay.StartUtc(databaseNow);
        var branchId = scope.BranchId ?? query.BranchId;

        var appointments = database.IntakeAppointments.AsNoTracking()
            .Where(item => branchId == null || item.BranchId == branchId);
        if (query.From is not null)
        {
            appointments = appointments.Where(item => item.StartsAt >= query.From && item.StartsAt < query.Until);
        }
        var rows = appointments
            .Select(item => new
            {
                Appointment = item,
                CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id),
                Cancelled = database.AppointmentCancellations.Any(cancellation => cancellation.AppointmentId == item.Id),
                NoShow = item.StartsAt < today &&
                    !database.AppointmentCancellations.Any(cancellation => cancellation.AppointmentId == item.Id) &&
                    !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id)
            });
        rows = query.Status switch
        {
            StaffWorklistStatus.Booked => rows.Where(row => !row.CheckedIn && !row.Cancelled && !row.NoShow),
            StaffWorklistStatus.CheckedIn => rows.Where(row => row.CheckedIn),
            StaffWorklistStatus.Cancelled => rows.Where(row => row.Cancelled),
            StaffWorklistStatus.NoShow => rows.Where(row => row.NoShow),
            _ => rows
        };
        var records = rows.Join(database.Branches, row => row.Appointment.BranchId, branch => branch.Id,
                (row, branch) => new { Row = row, BranchName = branch.Name })
            .Join(database.CustomerProfiles, row => row.Row.Appointment.OwnerId, customer => customer.UserId,
                (row, customer) => new WorklistRow
                {
                    Id = row.Row.Appointment.Id,
                    BranchId = row.Row.Appointment.BranchId,
                    BranchName = row.BranchName,
                    CustomerName = customer.LegalName,
                    RegistrationPlate = row.Row.Appointment.RegistrationPlate,
                    StartsAt = row.Row.Appointment.StartsAt,
                    EndsAt = row.Row.Appointment.EndsAt,
                    CheckedIn = row.Row.CheckedIn,
                    Cancelled = row.Row.Cancelled,
                    NoShow = row.Row.NoShow
                });
        if (query.Search.Length > 0)
        {
            var pattern = "%" + query.Search.Replace("!", "!!", StringComparison.Ordinal)
                .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";
            records = records.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.CustomerName, pattern, "!") || EF.Functions.ILike(row.BranchName, pattern, "!") ||
                EF.Functions.ILike(row.Id.ToString(), pattern, "!"));
        }
        return records;
    }

    public static StaffWorklistItem ToItem(WorklistRow row)
    {
        return new StaffWorklistItem(row.Id, row.BranchId, row.BranchName, row.CustomerName,
            row.RegistrationPlate, row.StartsAt, row.EndsAt, Status(row.CheckedIn, row.Cancelled, row.NoShow));
    }

    public static IOrderedQueryable<WorklistRow> Order(IQueryable<WorklistRow> rows, StaffWorklistQuery query)
    {
        return query.Sort switch
        {
            "registrationPlate" => OrderBy(rows, row => row.RegistrationPlate, query.Direction),
            "customerName" => OrderBy(rows, row => row.CustomerName, query.Direction),
            "branchName" => OrderBy(rows, row => row.BranchName, query.Direction),
            "status" => OrderStatus(rows, query.Direction),
            _ => OrderBy(rows, row => row.StartsAt, query.Direction)
        };
    }

    private static IOrderedQueryable<WorklistRow> OrderStatus(IQueryable<WorklistRow> rows, string direction)
    {
        if (direction == "desc")
        {
            return rows.OrderByDescending(row => row.NoShow).ThenByDescending(row => row.CheckedIn).ThenByDescending(row => row.Cancelled);
        }
        return rows.OrderBy(row => row.NoShow).ThenBy(row => row.CheckedIn).ThenBy(row => row.Cancelled);
    }

    private static string Status(bool checkedIn, bool cancelled, bool noShow)
    {
        if (checkedIn)
        {
            return nameof(StaffWorklistStatus.CheckedIn);
        }
        if (noShow)
        {
            return nameof(StaffWorklistStatus.NoShow);
        }

        return cancelled ? nameof(StaffWorklistStatus.Cancelled) : nameof(StaffWorklistStatus.Booked);
    }

    private static IOrderedQueryable<WorklistRow> OrderBy<TKey>(IQueryable<WorklistRow> rows,
        Expression<Func<WorklistRow, TKey>> key, string direction)
    {
        return direction == "desc" ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    public sealed class WorklistRow
    {
        public Guid Id { get; init; }
        public Guid BranchId { get; init; }
        public required string BranchName { get; init; }
        public required string CustomerName { get; init; }
        public required string RegistrationPlate { get; init; }
        public DateTimeOffset StartsAt { get; init; }
        public DateTimeOffset EndsAt { get; init; }
        public bool CheckedIn { get; init; }
        public bool Cancelled { get; init; }
        public bool NoShow { get; init; }
    }
}
