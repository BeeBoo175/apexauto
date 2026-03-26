using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class ReservationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    ReservationContext context) : IReservationTransaction
{
    public ReservationContext Context => context;

    public async Task<ReservationStatus> CommitAsync(Reservation? reservation, CancellationToken cancellationToken)
    {
        await transaction.CreateSavepointAsync("reservation_confirmation", cancellationToken);
        if (reservation is not null)
        {
            database.Reservations.Add(reservation);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReservationStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return await RejectConfirmationAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { ConstraintName: "CK_Reservations_Eligibility" or "CK_Reservations_Terms" })
        {
            return await RejectConfirmationAsync(cancellationToken);
        }
    }

    private async Task<ReservationStatus> RejectConfirmationAsync(CancellationToken cancellationToken)
    {
        // An admission race must not roll back refunds reconciled before the attempted confirmation.
        await transaction.RollbackToSavepointAsync("reservation_confirmation", cancellationToken);
        database.ChangeTracker.Clear();
        await transaction.CommitAsync(cancellationToken);
        return ReservationStatus.Conflict;
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
