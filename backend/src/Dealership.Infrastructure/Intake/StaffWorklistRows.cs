using Dealership.Domain.Common;
using System.Linq.Expressions;
using Dealership.Application.Accounts;
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
        var rows = await FlagsAsync(database, staffId, query, cancellationToken);
        if (rows is null)
        {
            return null;
        }

        rows = query.Status switch
        {
            StaffWorklistStatus.Booked => rows.Where(Booked),
            StaffWorklistStatus.CheckedIn => rows.Where(row => row.CheckedIn),
            StaffWorklistStatus.Cancelled => rows.Where(row => row.Cancelled),
            StaffWorklistStatus.NoShow => rows.Where(row => row.NoShow),
            _ => rows
        };
        rows = query.Queue switch
        {
            "ToClaim" => rows.Where(row => !row.CheckedIn && !row.Cancelled && !row.NoShow && row.HandlerId == null),
            "MyWork" => rows.Where(row => !row.CheckedIn && !row.Cancelled && !row.NoShow && row.HandlerId == staffId),
            "History" => rows.Where(row => (row.CheckedIn || row.Cancelled || row.NoShow) && row.HandlerId == staffId),
            _ => rows
        };
        // Only the group scope, which has no branch of its own, chooses branches by name.
        var scope = await StaffIntakeReadScope.ResolveAsync(database, staffId, cancellationToken);
        return Records(database, rows, query, scope?.BranchId is null);
    }

    // Tab totals for the actor's branch scope, ignoring status, dates, search and column filters.
    public static async Task<StaffWorklistCounts?> CountsAsync(DealershipDbContext database,
        Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        var rows = await FlagsAsync(database, staffId, query with { From = null, Until = null },
            cancellationToken);
        if (rows is null)
        {
            return null;
        }

        return new StaffWorklistCounts(
            await rows.CountAsync(Booked, cancellationToken),
            await rows.CountAsync(row => row.CheckedIn, cancellationToken),
            await rows.CountAsync(row => row.Cancelled, cancellationToken),
            await rows.CountAsync(row => row.NoShow, cancellationToken),
            await rows.CountAsync(cancellationToken),
            await rows.CountAsync(row => !row.CheckedIn && !row.Cancelled && !row.NoShow && row.HandlerId == null, cancellationToken),
            await rows.CountAsync(row => !row.CheckedIn && !row.Cancelled && !row.NoShow && row.HandlerId == staffId, cancellationToken),
            await rows.CountAsync(row => (row.CheckedIn || row.Cancelled || row.NoShow) && row.HandlerId == staffId, cancellationToken));
    }

    private static readonly Expression<Func<AppointmentFlags, bool>> Booked =
        row => !row.CheckedIn && !row.Cancelled && !row.NoShow;

    private static async Task<IQueryable<AppointmentFlags>?> FlagsAsync(DealershipDbContext database,
        Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        var scope = await StaffIntakeReadScope.ResolveAsync(database, staffId, cancellationToken);

        if (scope is null ||
            (query.Queue.Length > 0 && scope.Role != AccountRoles.SalesAgent) ||
            (scope.BranchId is not null && query.BranchId is not null && query.BranchId != scope.BranchId))
        {
            return null;
        }

        var databaseNow = await database.DatabaseNowAsync(cancellationToken);
        var today = AppointmentDay.StartUtc(databaseNow);
        var branchId = scope.BranchId ?? query.BranchId;

        var appointments = database.IntakeAppointments.AsNoTracking()
            .Where(item => branchId == null || item.BranchId == branchId);
        if (query.From is not null)
        {
            appointments = appointments.Where(item => item.StartsAt >= query.From && item.StartsAt < query.Until);
        }
        return appointments
            .Select(item => new AppointmentFlags
            {
                Appointment = item,
                CheckedIn = database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id),
                Cancelled = item.CancelledAt != null,
                NoShow = item.StartsAt < today &&
                    item.CancelledAt == null &&
                    !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == item.Id),
                HandlerId = database.AppointmentAssignments.Where(assignment => assignment.AppointmentId == item.Id)
                    .OrderByDescending(assignment => assignment.Version).Select(assignment => assignment.HandlerId).FirstOrDefault(),
                OwnershipVersion = database.AppointmentAssignments.Where(assignment => assignment.AppointmentId == item.Id)
                    .OrderByDescending(assignment => assignment.Version).Select(assignment => (long?)assignment.Version).FirstOrDefault() ?? 0
            }).Where(row => query.HandlerId == null || row.HandlerId == query.HandlerId);
    }

    private static IQueryable<WorklistRow> Records(DealershipDbContext database,
        IQueryable<AppointmentFlags> rows, StaffWorklistQuery query, bool searchBranch)
    {
        var handlers = StaffHandlerRows.Eligible(database);
        var records = rows.Join(database.Branches, row => row.Appointment.BranchId, branch => branch.Id,
                (row, branch) => new { Row = row, BranchName = branch.Name })
            .Join(database.CustomerProfiles, row => row.Row.Appointment.OwnerId, customer => customer.UserId,
                (row, customer) => new WorklistRow
                {
                    Id = row.Row.Appointment.Id,
                    BranchId = row.Row.Appointment.BranchId,
                    BranchName = row.BranchName,
                    CustomerName = customer.LegalName,
                    OwnerId = row.Row.Appointment.OwnerId,
                    Mobile = customer.Mobile,
                    RegistrationPlate = row.Row.Appointment.RegistrationPlate,
                    Make = row.Row.Appointment.Valuation.Make,
                    Model = row.Row.Appointment.Valuation.Model,
                    Trim = row.Row.Appointment.Valuation.Trim,
                    ManufactureYear = row.Row.Appointment.Valuation.ManufactureYear,
                    StartsAt = row.Row.Appointment.StartsAt,
                    EndsAt = row.Row.Appointment.EndsAt,
                    CheckedIn = row.Row.CheckedIn,
                    Cancelled = row.Row.Cancelled,
                    NoShow = row.Row.NoShow,
                    HandlerId = row.Row.HandlerId,
                    OwnershipVersion = row.Row.OwnershipVersion,
                    HandlerEmail = database.Users.Where(user => user.Id == row.Row.HandlerId).Select(user => user.Email).FirstOrDefault(),
                    HandlerName = database.StaffProfiles.Where(profile => profile.UserId == row.Row.HandlerId)
                        .Select(profile => profile.Name).FirstOrDefault(),
                    HandlerPictureId = database.AccountPictures
                        .Where(picture => picture.UserId == row.Row.HandlerId && picture.RemovedAt == null)
                        .Select(picture => (Guid?)picture.Id).FirstOrDefault(),
                    HandlerEligible = handlers.Any(user => user.Id == row.Row.HandlerId &&
                        user.BranchId == row.Row.Appointment.BranchId)
                });
        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            // Vehicle, customer and contact; no identifier is matched.
            records = records.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.CustomerName, pattern, "!") ||
                EF.Functions.ILike(row.Mobile, pattern, "!") ||
                database.Users.Any(user => user.Id == row.OwnerId && EF.Functions.ILike(user.Email!, pattern, "!")) ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!") ||
                searchBranch && EF.Functions.ILike(row.BranchName, pattern, "!"));
        }
        if (query.Registration.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Registration);
            records = records.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!"));
        }
        if (query.Vehicle.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Vehicle);
            records = records.Where(row => EF.Functions.ILike(
                row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }
        if (query.Customer.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Customer);
            records = records.Where(row => EF.Functions.ILike(row.CustomerName, pattern, "!"));
        }
        if (query.Handler.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Handler);
            records = records.Where(row => row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!"));
        }
        return records;
    }

    public static StaffWorklistItem ToItem(WorklistRow row)
    {
        return new StaffWorklistItem(row.Id, row.BranchId, row.BranchName, row.CustomerName,
            row.RegistrationPlate, row.StartsAt, row.EndsAt, Status(row.CheckedIn, row.Cancelled, row.NoShow),
            row.Make, row.Model, row.Trim, row.ManufactureYear,
            row.HandlerId, row.HandlerEmail, row.HandlerEligible, row.OwnershipVersion,
            row.HandlerName, row.HandlerPictureId);
    }

    public static IOrderedQueryable<WorklistRow> Order(IQueryable<WorklistRow> rows, StaffWorklistQuery query)
    {
        return query.Sort switch
        {
            "registrationPlate" => OrderBy(rows, row => row.RegistrationPlate, query.Direction),
            "vehicle" => OrderBy(rows, row => row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, query.Direction),
            "customerName" => OrderBy(rows, row => row.CustomerName, query.Direction),
            "handlerEmail" => OrderBy(rows, row => row.HandlerEmail, query.Direction),
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

    private sealed class AppointmentFlags
    {
        public required IntakeAppointment Appointment { get; init; }
        public bool CheckedIn { get; init; }
        public bool Cancelled { get; init; }
        public bool NoShow { get; init; }
        public Guid? HandlerId { get; init; }
        public long OwnershipVersion { get; init; }
    }

    public sealed class WorklistRow
    {
        public Guid Id { get; init; }
        public Guid BranchId { get; init; }
        public required string BranchName { get; init; }
        public required string CustomerName { get; init; }
        public Guid OwnerId { get; init; }
        public string Mobile { get; init; } = string.Empty;
        public required string RegistrationPlate { get; init; }
        public required string Make { get; init; }
        public required string Model { get; init; }
        public required string Trim { get; init; }
        public int ManufactureYear { get; init; }
        public DateTimeOffset StartsAt { get; init; }
        public DateTimeOffset EndsAt { get; init; }
        public bool CheckedIn { get; init; }
        public bool Cancelled { get; init; }
        public bool NoShow { get; init; }
        public Guid? HandlerId { get; init; }
        public string? HandlerEmail { get; init; }
        public string? HandlerName { get; init; }
        public Guid? HandlerPictureId { get; init; }
        public bool HandlerEligible { get; init; }
        public long OwnershipVersion { get; init; }
    }
}
