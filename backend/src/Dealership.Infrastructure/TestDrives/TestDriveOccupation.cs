namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveOccupation
{
    public Guid StartId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? OutcomeId { get; private set; }
}
