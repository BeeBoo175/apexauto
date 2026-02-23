using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Intake;

internal sealed class AppointmentCancellationTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    CancellationContext context, string? recipient, MailDeliverySignal mail, PhotoCleanupSignal cleanup) : IAppointmentCancellationTransaction
{
    public CancellationContext Context => context;

    public async Task<CancellationStatus> CommitAsync(IntakeAppointment appointment, CancellationToken cancellationToken)
    {
        database.AppointmentMailOutbox.Add(new AppointmentMail(appointment.Id, AppointmentMailKind.Cancellation,
            recipient!, appointment.CancelledAt!.Value));

        try
        {
            await database.IntakeAppointments.Where(item => item.Id == appointment.Id)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.CancelledAt, appointment.CancelledAt)
                    .SetProperty(item => item.CancellationPhotosExpireAt, appointment.CancellationPhotosExpireAt), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            mail.Request();
            cleanup.Request();
            return CancellationStatus.Succeeded;
        }
        catch (PostgresException exception) when (exception.ConstraintName is
            "CK_IntakeAppointments_CancellationTime" or "CK_IntakeAppointments_CheckedIn")
        {
            return exception.ConstraintName == "CK_IntakeAppointments_CheckedIn"
                ? CancellationStatus.CheckedIn
                : CancellationStatus.TooLate;
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
            // Disposal rolls back incomplete work; discard any uncommitted cancellation mail intent.
            database.ChangeTracker.Clear();
        }
    }
}
