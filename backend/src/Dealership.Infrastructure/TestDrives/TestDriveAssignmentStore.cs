using Dealership.Application.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveAssignmentStore(DealershipDbContext database) : ITestDriveAssignmentStore
{
    public async Task<ITestDriveAssignmentTransaction> LockAsync(Guid actorId, Guid bookingId,
        TestDriveAssignmentRequest request, CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"test-drive-assignment:" + request.OperationId}, 0))", cancellationToken);
            var users = await database.Users.FromSqlInterpolated($"""
                SELECT * FROM "AspNetUsers" WHERE "Id" = {actorId} OR "Id" = {request.HandlerId}
                ORDER BY "Id" FOR SHARE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var actor = users.SingleOrDefault(user => user.Id == actorId);
            var handler = users.SingleOrDefault(user => user.Id == request.HandlerId);
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

            var existing = await database.TestDriveAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            var current = await database.TestDriveAssignments.AsNoTracking()
                .Where(item => item.BookingId == bookingId).OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            var cancelled = await database.TestDriveCancellations.AnyAsync(item => item.BookingId == bookingId, cancellationToken);
            var context = new TestDriveAssignmentContext(booking,
                actor is null ? null : new TestDriveStaff(actor.IsActive, actor.Role, actor.BranchId),
                handler is null ? null : new TestDriveStaff(handler.IsActive, handler.Role, handler.BranchId),
                branchActive, cancelled, existing, current);
            return new TestDriveAssignmentTransaction(database, transaction, context);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
