using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveCancellationStore(DealershipDbContext database, MailDeliverySignal mail) : ITestDriveCancellationStore
{
    public async Task<ITestDriveCancellationTransaction> LockAsync(Guid customerId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"test-drive-cancellation:" + operationId}, 0))", cancellationToken);
            var customer = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var profile = await database.CustomerProfiles.FromSqlInterpolated(
                $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (customer is not { IsActive: true, Role: AccountRoles.Customer } || profile is null)
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(TestDriveCancellationStatus.Forbidden, default), string.Empty);
            }

            var existing = await database.TestDriveCancellations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            if (existing is not null)
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(TestDriveCancellationStatus.Ready, default, Existing: existing), customer.Email!);
            }

            var booking = await database.TestDriveBookings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == bookingId && item.CustomerId == customerId, cancellationToken);
            if (booking is null)
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(TestDriveCancellationStatus.NotFound, default), customer.Email!);
            }

            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {booking.BranchId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {booking.VehicleId} FOR UPDATE", cancellationToken);
            var cancelled = await database.TestDriveCancellations.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            return new TestDriveCancellationTransaction(database, transaction, mail,
                new(TestDriveCancellationStatus.Ready, now, booking, Cancelled: cancelled), customer.Email!);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
