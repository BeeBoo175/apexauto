using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveCancellationStore(DealershipDbContext database, MailDeliverySignal mail) : ITestDriveCancellationStore
{
    public async Task<ITestDriveCancellationTransaction> LockAsync(Guid actorId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"test-drive-cancellation:" + operationId}, 0))", cancellationToken);
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var customerCommand = actor?.Role == AccountRoles.Customer;
            var hasProfile = !customerCommand || await database.CustomerProfiles.FromSqlInterpolated(
                $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {actorId} FOR SHARE")
                .AnyAsync(cancellationToken);
            if (actor is not { IsActive: true } || !hasProfile ||
                actor.Role is not (AccountRoles.Customer or AccountRoles.SalesAgent or AccountRoles.BranchManager))
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(TestDriveCancellationStatus.Forbidden, default), string.Empty);
            }

            var existing = await database.TestDriveCancellations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var staff = new BranchStaff(actor.IsActive, actor.Role, actor.BranchId);
            if (existing is not null && customerCommand)
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(TestDriveCancellationStatus.Ready, default, Existing: existing, Actor: staff), actor.Email!);
            }

            var branchActive = false;
            if (!customerCommand && actor.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR UPDATE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            var booking = await database.TestDriveBookings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
            if (booking is null || customerCommand && booking.CustomerId != actorId ||
                !customerCommand && booking.BranchId != actor.BranchId)
            {
                return new TestDriveCancellationTransaction(database, transaction, mail,
                    new(existing is null ? TestDriveCancellationStatus.NotFound : TestDriveCancellationStatus.Ready,
                        default, Existing: existing, Actor: staff, BranchActive: branchActive), string.Empty);
            }

            if (customerCommand)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {booking.BranchId} FOR UPDATE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {booking.VehicleId} FOR UPDATE", cancellationToken);
            var cancelled = await database.TestDriveCancellations.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            var current = await database.TestDriveAssignments.AsNoTracking()
                .Where(item => item.BookingId == bookingId).OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            var recipient = await database.Users.Where(item => item.Id == booking.CustomerId)
                .Select(item => item.Email!).SingleAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            var started = await database.TestDriveStarts.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            var completed = await database.TestDriveOutcomes.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            return new TestDriveCancellationTransaction(database, transaction, mail,
                new(TestDriveCancellationStatus.Ready, now, booking, existing, cancelled, staff, branchActive, current,
                    started, completed), recipient);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
