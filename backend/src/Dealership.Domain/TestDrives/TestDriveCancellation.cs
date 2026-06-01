namespace Dealership.Domain.TestDrives;

public static class TestDriveCancellationKind
{
    public const string CustomerCancelled = "CustomerCancelled";
    public const string StaffCancelled = "StaffCancelled";
    public const string VehicleReserved = "VehicleReserved";
    public const string ListingWithdrawn = "ListingWithdrawn";
    public const string InspectionReopened = "InspectionReopened";
    public const string InspectionRepairRequested = "InspectionRepairRequested";
}

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
        Kind = TestDriveCancellationKind.CustomerCancelled;
        RecordedAt = recordedAt;
    }

    public static TestDriveCancellation ByStaff(Guid id, TestDriveBooking booking, Guid staffId,
        string reason, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(booking);
        reason = NormalizeReason(reason);
        if (id == Guid.Empty || staffId == Guid.Empty || recordedAt.Offset != TimeSpan.Zero ||
            recordedAt < booking.CreatedAt || recordedAt == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("Staff cancellation requires an actor and a finite UTC recording time.");
        }

        return new TestDriveCancellation
        {
            Id = id,
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            Kind = TestDriveCancellationKind.StaffCancelled,
            StaffId = staffId,
            Reason = reason,
            RecordedAt = recordedAt
        };
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public Guid? StaffId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? ListingWithdrawalId { get; private set; }
    public Guid? InspectionReopeningId { get; private set; }
    public Guid? InspectionRepairRequestId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || reason.Any(char.IsControl))
        {
            throw new ArgumentException("Cancellation requires a reason of up to 1000 non-control characters.");
        }

        return reason.Trim();
    }
}
