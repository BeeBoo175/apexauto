using Dealership.Domain.Common;

namespace Dealership.Domain.TestDrives;

public sealed class TestDriveBooking
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

    private TestDriveBooking()
    {
    }

    public TestDriveBooking(Guid id, Guid customerId, Guid vehicleId, Guid branchId, Guid publicationId,
        AppointmentSlot slot, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || vehicleId == Guid.Empty ||
            branchId == Guid.Empty || publicationId == Guid.Empty || slot.StartsAt <= createdAt ||
            slot.EndsAt - slot.StartsAt != Duration || createdAt == DateTimeOffset.MinValue ||
            createdAt == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("A test-drive booking requires an owned vehicle publication and future thirty-minute slot.");
        }

        Id = id;
        CustomerId = customerId;
        VehicleId = vehicleId;
        BranchId = branchId;
        PublicationId = publicationId;
        StartsAt = slot.StartsAt.ToUniversalTime();
        EndsAt = slot.EndsAt.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid PublicationId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? BookedByStaffId { get; private set; }

    // Set when a staff member made the booking for the customer, so the record still names who acted.
    public void RecordAssistance(Guid staffId)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("Assisting staff require an identity.", nameof(staffId));
        }

        BookedByStaffId = staffId;
    }
}
