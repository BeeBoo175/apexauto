namespace Dealership.Domain.TestDrives;

public sealed class TestDriveCancellation
{
    private TestDriveCancellation()
    {
    }

    public TestDriveCancellation(Guid id, TestDriveBooking booking, Guid customerId, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(booking);
        if (id == Guid.Empty || customerId != booking.CustomerId || recordedAt.Offset != TimeSpan.Zero ||
            recordedAt < booking.CreatedAt || recordedAt >= booking.StartsAt)
        {
            throw new ArgumentException("Customer cancellation requires the booking owner and a UTC time before booked start.");
        }

        Id = id;
        BookingId = booking.Id;
        CustomerId = customerId;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
}
