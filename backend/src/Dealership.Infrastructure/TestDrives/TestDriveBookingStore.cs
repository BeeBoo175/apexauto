using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveBookingStore(DealershipDbContext database, MailDeliverySignal mail) : ITestDriveBookingStore
{
    public async Task<ITestDriveBookingTransaction> LockAsync(Guid customerId, Guid vehicleId,
        TestDriveBookingRequest request, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"test-drive-booking:" + request.OperationId}, 0))", cancellationToken);
            var customer = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var profile = await database.CustomerProfiles.FromSqlInterpolated(
                $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (customer is not { IsActive: true, EmailConfirmed: true, Role: AccountRoles.Customer } || profile is null)
            {
                return new TestDriveBookingTransaction(database, transaction, mail,
                    new(TestDriveBookingStatus.Forbidden, default), string.Empty);
            }

            var existing = await database.TestDriveBookings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                return new TestDriveBookingTransaction(database, transaction, mail,
                    new(TestDriveBookingStatus.Ready, default, Existing: existing), customer.Email!);
            }

            var branchId = await database.Vehicles.Where(item => item.Id == vehicleId)
                .Select(item => (Guid?)item.BranchId).SingleOrDefaultAsync(cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {branchId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE", cancellationToken);
            var publication = await PublicListingQuery.Read(database)
                .Where(item => item.Vehicle.Id == vehicleId && item.Publication.Id == request.PublicationId)
                .Select(item => item.Publication).SingleOrDefaultAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            var today = AppointmentDay.StartUtc(now);
            var hasBooking = await database.TestDriveBookings.AnyAsync(item => item.CustomerId == customerId &&
                (item.StartsAt >= today || TestDriveOccupationRows.Active(database).Any(occupation =>
                    occupation.BookingId == item.Id)) &&
                !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id) &&
                !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id), cancellationToken);
            var endsAt = request.StartsAt + TestDriveBooking.Duration;
            var occupied = await database.TestDriveBookings.AnyAsync(item => item.BranchId == branchId &&
                item.StartsAt < endsAt && item.EndsAt > request.StartsAt &&
                !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == item.Id) &&
                !database.TestDriveCancellations.Any(cancellation => cancellation.BookingId == item.Id), cancellationToken);
            var status = publication is null ? TestDriveBookingStatus.VehicleUnavailable : TestDriveBookingStatus.Ready;
            return new TestDriveBookingTransaction(database, transaction, mail,
                new(status, now, branchId ?? Guid.Empty, hasBooking, occupied), customer.Email!);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
