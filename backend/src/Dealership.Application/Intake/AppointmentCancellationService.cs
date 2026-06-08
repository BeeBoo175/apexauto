using Dealership.Application.Accounts;
using Dealership.Domain.Common;

namespace Dealership.Application.Intake;

public sealed class AppointmentCancellationService(IAppointmentCancellationStore cancellations, TimeProvider clock)
{
    public async Task<CancellationResult> CancelAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken)
    {
        if (appointmentId == Guid.Empty)
        {
            return new CancellationResult(CancellationStatus.Invalid);
        }

        await using var transaction = await cancellations.LockAsync(ownerId, appointmentId, cancellationToken);
        var context = transaction.Context;

        if (context.Customer is not { IsActive: true, EmailConfirmed: true, Role: AccountRoles.Customer, HasProfile: true })
        {
            return new CancellationResult(CancellationStatus.Forbidden);
        }

        var appointment = context.Appointment;
        if (appointment is null)
        {
            return new CancellationResult(CancellationStatus.NotFound);
        }

        if (appointment.CancelledAt is not null)
        {
            return new CancellationResult(CancellationStatus.Succeeded,
                new CancellationReceipt(appointmentId, appointment.CancelledAt.Value));
        }

        if (context.CheckedIn)
        {
            return new CancellationResult(CancellationStatus.CheckedIn);
        }

        var now = UtcTime.TruncateToMicroseconds(clock.GetUtcNow());

        if (now < appointment.CreatedAt || now >= appointment.StartsAt)
        {
            return new CancellationResult(CancellationStatus.TooLate);
        }

        appointment.Cancel(now, context.PhotoRetention);
        var status = await transaction.CommitAsync(appointment, cancellationToken);
        return new CancellationResult(status,
            status == CancellationStatus.Succeeded ? new CancellationReceipt(appointmentId, now) : null);
    }
}
