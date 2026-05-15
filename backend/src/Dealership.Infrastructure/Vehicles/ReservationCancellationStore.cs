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
            if (user is not { IsActive: true, Role: AccountRoles.Customer })
            {
                return new ReservationCancellationTransaction(database, transaction,
                    new(actor, false, null, null, null, false, default));
            }

            const bool branchActive = true;
            var reservation = await database.Reservations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == reservationId &&
                item.CustomerId == actorId, cancellationToken);
            if (reservation is null)
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
            var now = await database.DatabaseNowAsync(cancellationToken);
            var sale = await database.SalesOrders.FromSqlInterpolated(
                $"SELECT * FROM \"SalesOrders\" WHERE \"Id\" = {reservationId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            now = sale is not null && now < sale.UpdatedAt ? sale.UpdatedAt : now;
            return new ReservationCancellationTransaction(database, transaction,
                new(actor, branchActive, reservation, release, existing, isHold, now) { Sale = sale });
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
