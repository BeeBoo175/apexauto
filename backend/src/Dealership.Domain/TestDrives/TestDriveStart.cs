using Dealership.Domain.Common;

namespace Dealership.Domain.TestDrives;

public sealed class TestDriveStart
{
    private TestDriveStart()
    {
    }

    public TestDriveStart(Guid id, TestDriveBooking booking, TestDriveAssignment assignment,
        Guid staffId, bool customerPresent, bool drivingLicenceChecked, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentNullException.ThrowIfNull(assignment);
        if (id == Guid.Empty || staffId == Guid.Empty || assignment.BookingId != booking.Id ||
            assignment.BranchId != booking.BranchId || assignment.HandlerId != staffId ||
            !customerPresent || !drivingLicenceChecked || startedAt.Offset != TimeSpan.Zero ||
            startedAt < booking.CreatedAt || startedAt < assignment.RecordedAt ||
            startedAt < AppointmentDay.StartUtc(booking.StartsAt) ||
            startedAt >= AppointmentDay.EndsAt(booking.StartsAt))
        {
            throw new ArgumentException("Start requires the current handler, attendance and licence affirmation on the booked Malaysian date.");
        }

        Id = id;
        BookingId = booking.Id;
        BranchId = booking.BranchId;
        VehicleId = booking.VehicleId;
        AssignmentId = assignment.Id;
        StaffId = staffId;
        CustomerPresent = customerPresent;
        DrivingLicenceChecked = drivingLicenceChecked;
        StartedAt = startedAt;
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StaffId { get; private set; }
    public bool CustomerPresent { get; private set; }
    public bool DrivingLicenceChecked { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
}
