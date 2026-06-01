using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveHandlingStore(DealershipDbContext database) : ITestDriveHandlingStore
{
    public async Task<ITestDriveHandlingTransaction> LockAsync(Guid actorId, Guid bookingId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"test-drive-handling:" + operationId}, 0))", cancellationToken);
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var branchActive = false;
            if (actor?.BranchId is not null)
            {
                branchActive = await database.Branches.FromSqlInterpolated(
                    $"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR UPDATE")
                    .AnyAsync(branch => branch.IsActive, cancellationToken);
            }

            var booking = await database.TestDriveBookings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
            if (booking is not null && booking.BranchId == actor?.BranchId)
            {
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {booking.VehicleId} FOR UPDATE", cancellationToken);
            }

            var current = await database.TestDriveAssignments.AsNoTracking()
                .Where(item => item.BookingId == bookingId).OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            var start = await database.TestDriveStarts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
            var outcome = await database.TestDriveOutcomes.AsNoTracking()
                .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
            var existingStart = await database.TestDriveStarts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var existingOutcome = await database.TestDriveOutcomes.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var cancelled = await database.TestDriveCancellations.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            var context = new TestDriveHandlingContext(booking,
                actor is null ? null : new TestDriveStaff(actor.IsActive, actor.Role, actor.BranchId),
                branchActive, cancelled, current, start, outcome, existingStart, existingOutcome);
            return new TestDriveHandlingTransaction(database, transaction, context);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
