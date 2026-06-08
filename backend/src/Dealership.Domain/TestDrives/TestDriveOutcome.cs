using Dealership.Domain.Common;

namespace Dealership.Domain.TestDrives;

public static class TestDriveOutcomeKind
{
    public const string Completed = "Completed";
    public const string NotCompleted = "NotCompleted";
}

public sealed class TestDriveOutcome
{
    private TestDriveOutcome()
    {
    }

    public TestDriveOutcome(Guid id, TestDriveBooking booking, TestDriveAssignment assignment,
        Guid staffId, TestDriveStart? start, string kind, bool customerPresent,
        string? reason, string? notes, DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentNullException.ThrowIfNull(assignment);
        reason = WorkOwnership.NormalizeReason(reason);
        notes = NormalizeNotes(notes);
        if (id == Guid.Empty || staffId == Guid.Empty || assignment.BookingId != booking.Id ||
            assignment.BranchId != booking.BranchId || assignment.HandlerId != staffId ||
            !customerPresent || endedAt.Offset != TimeSpan.Zero ||
            endedAt == DateTimeOffset.MaxValue || endedAt < booking.CreatedAt ||
            endedAt < assignment.RecordedAt)
        {
            throw new ArgumentException("Outcome requires attendance, the current handler and a finite UTC recording time.");
        }

        var valid = kind switch
        {
            TestDriveOutcomeKind.Completed => start is not null && reason is null,
            TestDriveOutcomeKind.NotCompleted => reason is not null,
            _ => false
        };
        if (!valid || start is not null && (start.BookingId != booking.Id ||
            start.VehicleId != booking.VehicleId || start.BranchId != booking.BranchId ||
            endedAt < start.StartedAt) || start is null &&
            (endedAt < AppointmentDay.StartUtc(booking.StartsAt) ||
                endedAt >= AppointmentDay.EndsAt(booking.StartsAt)))
        {
            throw new ArgumentException("Outcome must match its start or record a reasoned attended failure on the booked Malaysian date.");
        }

        Id = id;
        BookingId = booking.Id;
        BranchId = booking.BranchId;
        AssignmentId = assignment.Id;
        StaffId = staffId;
        StartId = start?.Id;
        Kind = kind;
        CustomerPresent = customerPresent;
        Reason = reason;
        Notes = notes;
        EndedAt = endedAt;
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid StaffId { get; private set; }
    public Guid? StartId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public bool CustomerPresent { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }

    public static string? NormalizeNotes(string? notes)
    {
        return BoundedText.NormalizeOptional(notes,
            "Outcome notes require up to 1000 non-control characters.");
    }
}
