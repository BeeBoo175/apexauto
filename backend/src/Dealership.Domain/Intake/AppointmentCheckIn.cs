using Dealership.Domain.Common;

namespace Dealership.Domain.Intake;

public sealed class AppointmentCheckIn
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    private AppointmentCheckIn()
    {
    }

    public AppointmentCheckIn(Guid vehicleId, IntakeAppointment appointment, Guid staffId,
        CheckInEvidence evidence, DateTimeOffset now, DateTimeOffset? correctionRecordedAt = null,
        string? correctionReason = null)
    {
        var sameDate = TimeZoneInfo.ConvertTime(now, Malaysia).Date ==
            TimeZoneInfo.ConvertTime(appointment.StartsAt, Malaysia).Date;

        if (vehicleId == Guid.Empty || staffId == Guid.Empty || now < appointment.CreatedAt || !sameDate)
        {
            throw new ArgumentException("Check-in requires a vehicle, acting staff and the booked Malaysian calendar date.");
        }

        var normalized = evidence.Normalize();
        if (correctionRecordedAt is not null)
        {
            if (correctionRecordedAt < AppointmentDay.EndsAt(appointment.StartsAt))
            {
                throw new ArgumentException("No-show correction requires the booked day to have ended.");
            }

            CorrectionReason = NormalizeCorrectionReason(correctionReason ?? string.Empty);
            CorrectionRecordedAt = correctionRecordedAt.Value.ToUniversalTime();
        }
        else if (correctionReason is not null)
        {
            throw new ArgumentException("A correction reason requires its recording time.");
        }

        AppointmentId = appointment.Id;
        OwnerId = appointment.OwnerId;
        BranchId = appointment.BranchId;
        VehicleId = vehicleId;
        StaffId = staffId;
        CheckedInAt = now.ToUniversalTime();
        Vin = normalized.Vin!;
        MileageKilometres = normalized.MileageKilometres;
        CustomerPresent = normalized.CustomerPresent;
        IdentityConfirmed = normalized.IdentityConfirmed;
        OwnershipConfirmed = normalized.OwnershipConfirmed;
        VehicleConfirmed = normalized.VehicleConfirmed;
        Notes = normalized.Notes;
    }

    public Guid AppointmentId { get; private set; }

    public Guid OwnerId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid StaffId { get; private set; }

    public DateTimeOffset CheckedInAt { get; private set; }

    public string Vin { get; private set; } = string.Empty;

    public int MileageKilometres { get; private set; }

    public bool CustomerPresent { get; private set; }

    public bool IdentityConfirmed { get; private set; }

    public bool OwnershipConfirmed { get; private set; }

    public bool VehicleConfirmed { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset? CorrectionRecordedAt { get; private set; }

    public string? CorrectionReason { get; private set; }

    public static string NormalizeCorrectionReason(string reason)
    {
        var value = reason.Trim();
        if (value.Length is 0 or > 1000 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Provide a correction reason of at most 1000 characters without control characters.");
        }

        return value;
    }

    public bool Matches(CheckInEvidence evidence)
    {
        return evidence.Normalize() == new CheckInEvidence(Vin, MileageKilometres, CustomerPresent,
            IdentityConfirmed, OwnershipConfirmed, VehicleConfirmed, Notes);
    }
}
