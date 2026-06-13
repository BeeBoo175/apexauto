namespace Dealership.Infrastructure.TestDrives;

// Behavior-free projection maintained by the zz_test_drive_occupation_guard triggers so every vehicle-state
// writer shares one occupation fact. It lives in persistence because Domain never mutates it.
public sealed class TestDriveOccupation
{
    public Guid StartId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? OutcomeId { get; private set; }
}
