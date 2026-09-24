using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

internal static class StaffTestDriveRows
{
    public static async Task<CurrentActor?> ActorAsync(DealershipDbContext database, Guid staffId,
        CancellationToken cancellationToken)
    {
        return await database.Users.AsNoTracking()
            .Where(user => user.Id == staffId && user.IsActive &&
                (user.Role == AccountRoles.GeneralManager ||
                    (user.Role == AccountRoles.SalesAgent || user.Role == AccountRoles.BranchManager) &&
                    database.Branches.Any(branch => branch.Id == user.BranchId && branch.IsActive)))
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static IQueryable<StaffTestDriveRow> Create(DealershipDbContext database, Guid? branchId,
        DateTimeOffset today)
    {
        var bookings = database.TestDriveBookings.AsNoTracking()
            .Where(booking => branchId == null || booking.BranchId == branchId);
        var handlers = StaffHandlerRows.Eligible(database);
        var staff = PersonRows.Staff(database);
        return from booking in bookings
               join vehicle in database.Vehicles on booking.VehicleId equals vehicle.Id
               join branch in database.Branches on booking.BranchId equals branch.Id
               join customer in database.CustomerProfiles on booking.CustomerId equals customer.UserId
               join state in TestDriveStatusRows.Create(database, today) on booking.Id equals state.Id
               let assignment = database.TestDriveAssignments.Where(item => item.BookingId == booking.Id)
                   .OrderByDescending(item => item.Version).FirstOrDefault()
               select new StaffTestDriveRow
               {
                   Id = booking.Id,
                   VehicleId = booking.VehicleId,
                   BranchId = booking.BranchId,
                   BranchName = branch.Name,
                   RegistrationPlate = vehicle.RegistrationPlate,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   Trim = vehicle.Trim,
                   ManufactureYear = vehicle.ManufactureYear,
                   CustomerName = customer.LegalName ?? PersonLabels.FormerCustomer,
                   CustomerId = booking.CustomerId,
                   StartsAt = booking.StartsAt,
                   EndsAt = booking.EndsAt,
                   Status = state.Status,
                   AssignmentId = assignment == null ? null : assignment.Id,
                   OwnershipVersion = assignment == null ? 0 : assignment.Version,
                   HandlerId = assignment == null ? null : assignment.HandlerId,
                   HandlerEmail = database.Users.Where(user => user.Id == assignment!.HandlerId).Select(user => user.Email).FirstOrDefault(),
                   HandlerName = staff.Where(person => person.UserId == assignment!.HandlerId)
                       .Select(person => person.Name).FirstOrDefault(),
                   HandlerPictureId = staff.Where(person => person.UserId == assignment!.HandlerId)
                       .Select(person => person.PictureId).FirstOrDefault(),
                   HandlerEligible = handlers.Any(user => user.Id == assignment!.HandlerId &&
                       user.BranchId == booking.BranchId),
                   HandlerSuspended = database.Users.Any(user => user.Id == assignment!.HandlerId &&
                       user.SuspendedAt != null),
                   CustomerSuspended = database.Users.Any(user => user.Id == booking.CustomerId &&
                       user.SuspendedAt != null)
               };
    }

    public static IQueryable<StaffTestDriveRow> Queue(IQueryable<StaffTestDriveRow> rows,
        string queue, Guid staffId)
    {
        return queue switch
        {
            "toClaim" => rows.Where(row => row.Status == TestDriveStatus.Booked && row.HandlerId == null),
            "myWork" => rows.Where(row => (row.Status == TestDriveStatus.Booked || row.Status == TestDriveStatus.InProgress) && row.HandlerId == staffId),
            "history" => rows.Where(row => row.Status != TestDriveStatus.Booked && row.Status != TestDriveStatus.InProgress && row.HandlerId == staffId),
            _ => rows
        };
    }

    public static IQueryable<StaffTestDriveRow> Filter(IQueryable<StaffTestDriveRow> rows, StaffTestDriveQuery query)
    {
        if (query.Dates.From is not null)
        {
            rows = rows.Where(row => row.StartsAt >= query.Dates.From && row.StartsAt < query.Dates.Until);
        }

        if (query.Status != "All")
        {
            rows = rows.Where(row => row.Status == query.Status);
        }

        if (query.HandlerId is not null)
        {
            rows = rows.Where(row => row.HandlerId == query.HandlerId);
        }

        if (query.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            rows = rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.CustomerName, pattern, "!") ||
                EF.Functions.ILike(row.Id.ToString(), pattern, "!") ||
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }

        if (query.Handler.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Handler);
            rows = rows.Where(row => row.HandlerEmail != null && EF.Functions.ILike(row.HandlerEmail, pattern, "!") ||
                row.HandlerName != null && EF.Functions.ILike(row.HandlerName, pattern, "!"));
        }

        if (query.Registration.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Registration);
            rows = rows.Where(row => EF.Functions.ILike(row.RegistrationPlate, pattern, "!"));
        }

        if (query.Vehicle.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Vehicle);
            rows = rows.Where(row =>
                EF.Functions.ILike(row.ManufactureYear + " " + row.Make + " " + row.Model + " " + row.Trim, pattern, "!"));
        }

        if (query.Customer.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(query.Customer);
            rows = rows.Where(row => EF.Functions.ILike(row.CustomerName, pattern, "!"));
        }

        return rows;
    }

    public static StaffTestDriveItem ToItem(StaffTestDriveRow row) => new(row.Id, row.VehicleId, row.BranchId,
        row.BranchName, row.RegistrationPlate, row.Make, row.Model, row.Trim, row.ManufactureYear,
        row.CustomerName, row.StartsAt, row.EndsAt, row.Status, row.AssignmentId, row.OwnershipVersion,
        row.HandlerId, row.HandlerEmail, row.HandlerEligible, row.HandlerName, row.HandlerPictureId,
        row.HandlerSuspended, row.CustomerSuspended, row.CustomerId);

    public static IOrderedQueryable<StaffTestDriveRow> Order(IQueryable<StaffTestDriveRow> rows,
        StaffTestDriveQuery query)
    {
        var descending = query.Direction == "desc";
        return query.Sort switch
        {
            "registrationPlate" => descending ? rows.OrderByDescending(row => row.RegistrationPlate) : rows.OrderBy(row => row.RegistrationPlate),
            "vehicle" => descending ? rows.OrderByDescending(row => row.Make).ThenByDescending(row => row.Model).ThenByDescending(row => row.Trim).ThenByDescending(row => row.ManufactureYear)
                : rows.OrderBy(row => row.Make).ThenBy(row => row.Model).ThenBy(row => row.Trim).ThenBy(row => row.ManufactureYear),
            "customerName" => descending ? rows.OrderByDescending(row => row.CustomerName) : rows.OrderBy(row => row.CustomerName),
            "branchName" => descending ? rows.OrderByDescending(row => row.BranchName) : rows.OrderBy(row => row.BranchName),
            "handlerEmail" => descending ? rows.OrderByDescending(row => row.HandlerEmail) : rows.OrderBy(row => row.HandlerEmail),
            "status" => descending ? rows.OrderByDescending(row => row.Status) : rows.OrderBy(row => row.Status),
            _ => descending ? rows.OrderByDescending(row => row.StartsAt) : rows.OrderBy(row => row.StartsAt)
        };
    }
}

internal sealed class StaffTestDriveRow
{
    public Guid Id { get; init; }

    public Guid VehicleId { get; init; }

    public Guid BranchId { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public string RegistrationPlate { get; init; } = string.Empty;

    public string Make { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Trim { get; init; } = string.Empty;

    public int ManufactureYear { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset EndsAt { get; init; }

    public string Status { get; init; } = string.Empty;

    public Guid? AssignmentId { get; init; }

    public long OwnershipVersion { get; init; }

    public Guid? HandlerId { get; init; }

    public string? HandlerEmail { get; init; }

    public string? HandlerName { get; init; }

    public Guid? HandlerPictureId { get; init; }

    public bool HandlerEligible { get; init; }

    public bool HandlerSuspended { get; init; }

    public bool CustomerSuspended { get; init; }

    public Guid CustomerId { get; init; }
}
