using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class ReservationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    MailDeliverySignal mail, ReservationExpirySignal expiry, ReservationContext context) : IReservationTransaction
{
    public ReservationContext Context => context;

    public async Task<ReservationStatus> CommitAsync(Reservation? reservation, CancellationToken cancellationToken)
    {
        await transaction.CreateSavepointAsync("reservation_confirmation", cancellationToken);
        if (reservation is not null)
        {
            // Inside the savepoint, so a rejected confirmation gives its number back.
            reservation.AssignNumber(await NextNumberAsync(reservation.CreatedAt.Year, cancellationToken));
            database.Reservations.Add(reservation);
            // Every confirmed hold is a sale from the start, so staff work one record from the first minute.
            var sale = SalesOrder.Open(reservation, Guid.NewGuid(), out var created);
            database.SalesOrders.Add(sale);
            database.SalesOrderEvents.Add(created);
            database.ReservationMailOutbox.Add(new ReservationMail(reservation));
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            expiry.Request();

            if (reservation is not null)
            {
                mail.Request();
            }

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

    private async Task<string> NextNumberAsync(int year, CancellationToken cancellationToken)
    {
        var number = await YearlyNumberAllocator.NextAsync<ReservationNumberCounter>(database, year, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return number;
    }

    private async Task<ReservationStatus> RejectConfirmationAsync(CancellationToken cancellationToken)
    {
        // An admission race must not roll back refunds reconciled before the attempted confirmation.
        await transaction.RollbackToSavepointAsync("reservation_confirmation", cancellationToken);
        database.ChangeTracker.Clear();
        await transaction.CommitAsync(cancellationToken);
        expiry.Request();
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
