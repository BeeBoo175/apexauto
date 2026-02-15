using Dealership.Domain.Intake;

namespace Dealership.Domain.Vehicles;

public enum VehicleStatus
{
    PendingInspection
}

public sealed class Vehicle
{
    private Vehicle()
    {
    }

    public Vehicle(IntakeAppointment appointment, AppointmentCheckIn checkIn)
    {
        if (checkIn.AppointmentId != appointment.Id || checkIn.OwnerId != appointment.OwnerId ||
            checkIn.BranchId != appointment.BranchId)
        {
            throw new ArgumentException("Vehicle intake must retain one appointment's owner, branch and valuation provenance.");
        }

        Id = checkIn.VehicleId;
        IntakeAppointmentId = appointment.Id;
        IntakeOwnerId = appointment.OwnerId;
        BranchId = appointment.BranchId;
        Vin = checkIn.Vin;
        RegistrationPlate = appointment.RegistrationPlate;
        RegistrationIdentity = string.Concat(RegistrationPlate.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
        Make = appointment.Valuation.Make;
        Model = appointment.Valuation.Model;
        Trim = appointment.Valuation.Trim;
        ManufactureYear = appointment.Valuation.ManufactureYear;
        MileageKilometres = checkIn.MileageKilometres;
        CreatedAt = checkIn.CheckedInAt;
    }

    public Guid Id { get; private set; }
    public Guid IntakeAppointmentId { get; private set; }
    public Guid IntakeOwnerId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Vin { get; private set; } = string.Empty;
    public string RegistrationPlate { get; private set; } = string.Empty;
    public string RegistrationIdentity { get; private set; } = string.Empty;
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string Trim { get; private set; } = string.Empty;
    public int ManufactureYear { get; private set; }
    public int MileageKilometres { get; private set; }
    public VehicleStatus Status { get; private set; } = VehicleStatus.PendingInspection;
    public DateTimeOffset CreatedAt { get; private set; }
}
