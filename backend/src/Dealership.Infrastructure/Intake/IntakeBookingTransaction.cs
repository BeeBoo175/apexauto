using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

internal sealed class IntakeBookingTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    BookingContext context, string recipient, MailDeliverySignal mail) : IIntakeBookingTransaction
{
    public BookingContext Context => context;

    public async Task<BookingResult> CommitAsync(IntakeAppointment appointment,
        IReadOnlyList<AppointmentPhoto> photos, CancellationToken cancellationToken)
    {
        database.IntakeAppointments.Add(appointment);
        database.AppointmentPhotos.AddRange(photos);
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointment.Id, AppointmentMailKind.Confirmation,
            recipient, appointment.CreatedAt));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
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
