using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Intake;

internal sealed class IntakeBookingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    BookingContext context, string recipient) : IIntakeBookingTransaction
{
    public BookingContext Context => context;

    public async Task<BookingResult> CommitAsync(IntakeAppointment appointment, SellerValuation valuation,
        IReadOnlyList<AppointmentPhoto> photos, CancellationToken cancellationToken)
    {
        database.IntakeAppointments.Add(appointment);
        database.SellerValuations.Add(valuation);
        database.AppointmentPhotos.AddRange(photos);
        database.AppointmentConfirmations.Add(new AppointmentConfirmation
        {
            Id = appointment.Id,
            RecipientEmail = recipient,
            NextDeliveryAt = appointment.CreatedAt
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_IntakeAppointments_BranchId_StartsAt" } or
            PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: "EX_IntakeAppointments_Slot" })
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BookingResult(BookingStatus.SlotUnavailable);
        }

        var receipt = await AppointmentReceiptReader.ReadAsync(database, appointment.Id, appointment.OwnerId, cancellationToken);
        return new BookingResult(BookingStatus.Succeeded, receipt);
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        database.ChangeTracker.Clear();
    }
}
