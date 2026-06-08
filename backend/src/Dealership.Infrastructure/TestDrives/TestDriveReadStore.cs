using Dealership.Domain.Common;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveReadStore(DealershipDbContext database) : ITestDriveReadStore
{
    public async Task<TestDriveSlotSnapshot?> SlotsAsync(Guid vehicleId, DateTimeOffset from,
        DateTimeOffset until, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var publication = await PublicListingQuery.Read(database).Where(row => row.Vehicle.Id == vehicleId)
            .Select(row => new { row.Vehicle.BranchId, row.Publication.Id }).SingleOrDefaultAsync(cancellationToken);
        if (publication is null)
        {
            return null;
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var occupied = await database.TestDriveBookings.AsNoTracking()
            .Where(item => item.BranchId == publication.BranchId && item.StartsAt < until && item.EndsAt > from &&
                !database.TestDriveCancellations.Any(cancel => cancel.BookingId == item.Id) &&
                !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id))
            .Select(item => new AppointmentSlot(item.StartsAt, item.EndsAt)).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(now, publication.BranchId, publication.Id, occupied);
    }

    public async Task<TestDriveHistoryResult> AccountAsync(Guid customerId, int page, ListDateRange dates,
        CancellationToken cancellationToken, string search = "")
    {
        if (!Pagination.IsValidAccount(page, Pagination.AccountPageSize) ||
            !ListDateRange.IsValid(dates.From, dates.Until) || !ListSearch.IsValid(search))
        {
            return new(TestDriveReadStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await database.Users.AsNoTracking()
            .Where(user => user.Id == customerId && user.IsActive && user.Role == AccountRoles.Customer)
            .Select(user => new CurrentActor(user.Id, user.Role, user.BranchId)).SingleOrDefaultAsync(cancellationToken);
        if (actor is null)
        {
            return new(TestDriveReadStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var today = AppointmentDay.StartUtc(now);
        var bookings = database.TestDriveBookings.AsNoTracking().Where(item => item.CustomerId == customerId);
        if (dates.From is not null)
        {
            bookings = bookings.Where(item => item.StartsAt >= dates.From && item.StartsAt < dates.Until);
        }

        if (search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(search);
            bookings = bookings.Where(booking => database.Vehicles.Any(vehicle => vehicle.Id == booking.VehicleId &&
                (EF.Functions.ILike(vehicle.ManufactureYear + " " + vehicle.Make + " " + vehicle.Model + " " + vehicle.Trim, pattern, "!") ||
                    EF.Functions.ILike(vehicle.RegistrationPlate, pattern, "!"))) ||
                database.Branches.Any(branch => branch.Id == booking.BranchId && EF.Functions.ILike(branch.Name, pattern, "!")) ||
                EF.Functions.ILike(booking.Id.ToString(), pattern, "!"));
        }

        var total = await bookings.CountAsync(cancellationToken);
        var rows = from booking in bookings
                   join vehicle in database.Vehicles on booking.VehicleId equals vehicle.Id
                   join branch in database.Branches on booking.BranchId equals branch.Id
                   join state in TestDriveStatusRows.Create(database, today) on booking.Id equals state.Id
                   join cancellation in database.TestDriveCancellations on booking.Id equals cancellation.BookingId into cancellations
                   from cancellation in cancellations.DefaultIfEmpty()
                   join start in database.TestDriveStarts on booking.Id equals start.BookingId into starts
                   from start in starts.DefaultIfEmpty()
                   join outcome in database.TestDriveOutcomes on booking.Id equals outcome.BookingId into outcomes
                   from outcome in outcomes.DefaultIfEmpty()
                   orderby booking.StartsAt descending, booking.Id descending
                   select new TestDriveHistoryItem(booking.Id, booking.VehicleId, booking.BranchId, branch.Name,
                       vehicle.Make, vehicle.Model, vehicle.Trim, vehicle.ManufactureYear, booking.StartsAt,
                       booking.EndsAt, booking.CreatedAt,
                       state.Status,
                       start == null ? null : start.StartedAt, outcome == null ? null : outcome.EndedAt,
                       cancellation == null ? null : cancellation.RecordedAt,
                       cancellation == null ? null : cancellation.Reason, outcome == null ? null : outcome.Reason);
        var items = await rows.ReadPageAsync(page, Pagination.AccountPageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(TestDriveReadStatus.Succeeded, new(actor, items, total, page, Pagination.AccountPageSize));
    }
}
