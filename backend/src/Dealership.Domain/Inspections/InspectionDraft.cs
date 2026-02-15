using Dealership.Domain.Vehicles;

namespace Dealership.Domain.Inspections;

public sealed class InspectionDraft
{
    private InspectionDraft()
    {
    }

    public InspectionDraft(Guid id, Vehicle vehicle, Guid technicianId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        if (id == Guid.Empty || technicianId == Guid.Empty || vehicle.Status != VehicleStatus.PendingInspection)
        {
            throw new ArgumentException("Inspection claim requires a pending vehicle and nonempty identities.");
        }

        Id = id;
        VehicleId = vehicle.Id;
        BranchId = vehicle.BranchId;
        TechnicianId = technicianId;
        CreatedAt = createdAt;
        AssignmentVersion = 1;
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long AssignmentVersion { get; private set; }

    public void Reassign(Guid technicianId, long expectedVersion)
    {
        if (technicianId == Guid.Empty || technicianId == TechnicianId || expectedVersion != AssignmentVersion ||
            AssignmentVersion == long.MaxValue)
        {
            throw new ArgumentException("Reassignment requires a different technician and the current assignment version.");
        }

        TechnicianId = technicianId;
        AssignmentVersion++;
    }
}
