using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Intake;

public sealed class AppointmentCheckInService(IAppointmentCheckInStore checkIns, TimeProvider clock)
{
    public Task<CheckInResult> CheckInAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(staffId, appointmentId, evidence, null, cancellationToken);
    }

    public Task<CheckInResult> CorrectNoShowAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        NoShowCorrectionRequest correction, CancellationToken cancellationToken)
    {
        return ExecuteAsync(staffId, appointmentId, evidence, correction, cancellationToken);
    }

    private async Task<CheckInResult> ExecuteAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        NoShowCorrectionRequest? correction, CancellationToken cancellationToken)
    {
        if (appointmentId == Guid.Empty || staffId == Guid.Empty)
        {
            return new CheckInResult(CheckInStatus.Invalid);
        }

        try
        {
            evidence = evidence.Normalize();
            if (correction is not null)
            {
                correction = correction with
                {
                    Reason = AppointmentCheckIn.NormalizeCorrectionReason(correction.Reason),
                    AttendedAt = UtcTime.TruncateToMicroseconds(correction.AttendedAt)
                };
            }
        }
        catch (ArgumentException)
        {
            return new CheckInResult(CheckInStatus.Invalid);
        }

        await using var transaction = await checkIns.LockAsync(staffId, appointmentId, cancellationToken);
        var context = transaction.Context;
        var staff = context.Staff;

        if (staff is not { IsActive: true, BranchId: not null } ||
            staff.Role is not (AccountRoles.SalesAgent or AccountRoles.BranchManager) ||
            (correction is not null && staff.Role != AccountRoles.BranchManager) || !context.BranchActive)
        {
            return new CheckInResult(CheckInStatus.Forbidden);
        }

        var appointment = context.Appointment;
        if (appointment is null || appointment.BranchId != staff.BranchId)
        {
            return new CheckInResult(CheckInStatus.NotFound);
        }

        if (context.Existing is not null)
        {
            return Matches(context.Existing, evidence, correction)
                ? new CheckInResult(CheckInStatus.Succeeded, Receipt(context.Existing))
                : new CheckInResult(CheckInStatus.Conflict);
        }

        if (context.Customer is not { IsActive: true, EmailConfirmed: true, Role: AccountRoles.Customer } ||
            appointment.CancelledAt is not null)
        {
            return new CheckInResult(CheckInStatus.Unavailable);
        }

        if (correction is null && context.HandlerId != staffId)
        {
            return new CheckInResult(CheckInStatus.Forbidden);
        }

        var now = correction is null ? clock.GetUtcNow() : await transaction.ReadDatabaseTimeAsync(cancellationToken);
        now = UtcTime.TruncateToMicroseconds(now);
        AppointmentCheckIn checkIn;

        try
        {
            if (correction is not null && now < AppointmentDay.EndsAt(appointment.StartsAt))
            {
                return new CheckInResult(CheckInStatus.Unavailable);
            }

            checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, staffId, evidence,
                correction?.AttendedAt ?? now, correction is null ? null : now, correction?.Reason);
        }
        catch (ArgumentException)
        {
            return new CheckInResult(CheckInStatus.Unavailable);
        }

        var photos = await transaction.LockPhotosAsync(cancellationToken);
        if (photos.Count != 4 || photos.Any(photo => photo.AvailableAt is null || photo.RemovalPending))
        {
            return new CheckInResult(CheckInStatus.Unavailable);
        }

        var status = await transaction.CommitAsync(new Vehicle(appointment, checkIn), checkIn, now, cancellationToken);
        return new CheckInResult(status, status == CheckInStatus.Succeeded ? Receipt(checkIn) : null);
    }

    private static bool Matches(AppointmentCheckIn existing, CheckInEvidence evidence, NoShowCorrectionRequest? correction)
    {
        if (correction is null)
        {
            return existing.Matches(evidence) && existing.CorrectionRecordedAt is null;
        }

        return existing.Matches(evidence) && existing.CorrectionRecordedAt is not null &&
            existing.CheckedInAt == correction.AttendedAt && existing.CorrectionReason == correction.Reason;
    }

    private static CheckInReceipt Receipt(AppointmentCheckIn checkIn)
    {
        return new CheckInReceipt(checkIn.AppointmentId, checkIn.VehicleId, checkIn.StaffId, checkIn.CheckedInAt,
            new CheckInEvidence(checkIn.Vin, checkIn.MileageKilometres, checkIn.CustomerPresent,
                checkIn.IdentityConfirmed, checkIn.OwnershipConfirmed, checkIn.VehicleConfirmed, checkIn.Notes),
            checkIn.CorrectionRecordedAt is null ? null : new CorrectionReceipt(checkIn.CorrectionRecordedAt.Value, checkIn.CorrectionReason!));
    }
}
