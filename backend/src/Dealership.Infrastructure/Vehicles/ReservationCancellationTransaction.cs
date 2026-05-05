using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class ReservationCancellationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    ReservationCancellationContext context) : IReservationCancellationTransaction
{
    public ReservationCancellationContext Context => context;

    public async Task<ReservationStatus> CommitAsync(ReservationRelease? release, Func<SalesOrderEvent>? close,
        CancellationToken cancellationToken)
    {
        await transaction.CreateSavepointAsync("reservation_cancellation", cancellationToken);
        if (release is not null)
        {
            database.ReservationReleases.Add(release);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (close is not null)
            {
                database.SalesOrderEvents.Add(close());
                await database.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return ReservationStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres &&
            (postgres.SqlState == PostgresErrorCodes.UniqueViolation || postgres.ConstraintName == "CK_ReservationReleases_Deadline"))
        {
            await transaction.RollbackToSavepointAsync("reservation_cancellation", cancellationToken);
            database.ChangeTracker.Clear();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT release_unavailable_reservation({context.Reservation!.VehicleId})", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReservationStatus.Conflict;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}
