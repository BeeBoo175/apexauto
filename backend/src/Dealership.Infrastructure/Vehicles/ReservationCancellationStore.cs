using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationCancellationStore(DealershipDbContext database) : IReservationCancellationStore
{
    public async Task<IReservationCancellationTransaction> LockAsync(Guid actorId, Guid reservationId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"reservation:" + operationId}, 0))", cancellationToken);
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var actor = user is null ? null : new ReservationActor(user.IsActive, user.Role, user.BranchId);
            if (user is not { IsActive: true } ||
                user.Role is not (AccountRoles.Customer or AccountRoles.BranchManager or AccountRoles.GeneralManager))
            {
                return new ReservationCancellationTransaction(database, transaction,
                    new(actor, false, null, null, null, false, default));
            }

            var branchActive = user.Role != AccountRoles.BranchManager || await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {user.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken);
            var reservation = await database.Reservations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == reservationId &&
                (user.Role != AccountRoles.Customer || item.CustomerId == actorId) &&
                (user.Role != AccountRoles.BranchManager || item.BranchId == user.BranchId), cancellationToken);
            if (reservation is null || !branchActive)
            {
                return new ReservationCancellationTransaction(database, transaction,
                    new(actor, branchActive, null, null, null, false, default));
            }

            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {reservation.VehicleId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT release_unavailable_reservation({reservation.VehicleId})", cancellationToken);
            reservation = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == reservationId, cancellationToken);
            var release = await database.ReservationReleases.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ReservationId == reservationId, cancellationToken);
            var existing = await database.ReservationReleases.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var isHold = await database.Reservations.AnyAsync(item => item.Id == operationId, cancellationToken);
            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            return new ReservationCancellationTransaction(database, transaction,
                new(actor, branchActive, reservation, release, existing, isHold, now));
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}
