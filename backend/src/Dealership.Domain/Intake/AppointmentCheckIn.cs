using Dealership.Domain.Common;

namespace Dealership.Domain.Intake;

public sealed class AppointmentCheckIn
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    private AppointmentCheckIn()
    {
    }

    public AppointmentCheckIn(Guid vehicleId, IntakeAppointment appointment, Guid staffId,
        CheckInEvidence evidence, DateTimeOffset now)
    {
        var sameDate = TimeZoneInfo.ConvertTime(now, Malaysia).Date ==
            TimeZoneInfo.ConvertTime(appointment.StartsAt, Malaysia).Date;

        if (vehicleId == Guid.Empty || staffId == Guid.Empty || now < appointment.CreatedAt || !sameDate)
        {
            throw new ArgumentException("Check-in requires a vehicle, acting staff and the booked Malaysian calendar date.");
        }

        var normalized = evidence.Normalize();
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

    public bool Matches(CheckInEvidence evidence)
    {
        return evidence.Normalize() == new CheckInEvidence(Vin, MileageKilometres, CustomerPresent,
            IdentityConfirmed, OwnershipConfirmed, VehicleConfirmed, Notes);
    }
}
