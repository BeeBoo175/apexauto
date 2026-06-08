using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;

namespace Dealership.UnitTests;

public sealed class TestDriveHandlingTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void StartRequiresAttendanceAndLicenceAffirmation(bool present, bool licenceChecked, bool accepted)
    {
        var booking = Booking();
        var assignment = Claim(booking);
        if (!accepted)
        {
            Assert.Throws<ArgumentException>(() => new TestDriveStart(Guid.NewGuid(), booking, assignment,
                assignment.HandlerId!.Value, present, licenceChecked, booking.StartsAt));
            return;
        }

        var start = Start(booking, assignment);
        Assert.Equal(booking.VehicleId, start.VehicleId);
        Assert.Equal(booking.BranchId, start.BranchId);
        Assert.Equal(booking.Id, start.BookingId);
        Assert.Equal(assignment.Id, start.AssignmentId);
        Assert.Equal(assignment.HandlerId, start.StaffId);
        Assert.True(start.CustomerPresent);
        Assert.True(start.DrivingLicenceChecked);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(863999999999, true)]
    [InlineData(864000000000, false)]
    public void StartUsesBookedMalaysianDateWithExclusiveEnd(long ticks, bool accepted)
    {
        var booking = Booking();
        var assignment = Claim(booking);
        var now = AppointmentDay.StartUtc(booking.StartsAt).AddTicks(ticks);
        if (!accepted)
        {
            Assert.Throws<ArgumentException>(() => Start(booking, assignment, now));
            return;
        }

        Assert.Equal(now, Start(booking, assignment, now).StartedAt);
    }

    [Fact]
    public void StartRejectsAnotherBookingHandlerOrFutureAssignment()
    {
        var booking = Booking();
        var assignment = Claim(booking);
        Assert.Throws<ArgumentException>(() => Start(Booking(), assignment));
        Assert.Throws<ArgumentException>(() => new TestDriveStart(Guid.NewGuid(), booking, assignment,
            Guid.NewGuid(), true, true, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Start(booking, assignment, assignment.RecordedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Start(booking, assignment, booking.StartsAt.ToOffset(TimeSpan.FromHours(8))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(14400)]
    public void CompletionHasNoPlannedEndOrDayExpiry(int minutesAfterStart)
    {
        var booking = Booking();
        var assignment = Claim(booking);
        var start = Start(booking, assignment);
        var endedAt = start.StartedAt.AddMinutes(minutesAfterStart);
        var outcome = Outcome(booking, assignment, start, TestDriveOutcomeKind.Completed, null, " Customer interested ", endedAt);

        Assert.Equal(start.Id, outcome.StartId);
        Assert.Equal(endedAt, outcome.EndedAt);
        Assert.Equal("Customer interested", outcome.Notes);
        Assert.Null(outcome.Reason);
        Assert.Equal(TestDriveOutcomeKind.Completed, outcome.Kind);
    }

    [Fact]
    public void HandoffPreservesOriginalStarterAndActualCompleter()
    {
        var booking = Booking();
        var claim = Claim(booking);
        var start = Start(booking, claim);
        var replacement = Guid.NewGuid();
        var handoff = new TestDriveAssignment(Guid.NewGuid(), booking, Guid.NewGuid(), TestDriveAssignmentKind.Assigned,
            replacement, claim, "Original handler unavailable", start.StartedAt.AddDays(2));
        var outcome = Outcome(booking, handoff, start, TestDriveOutcomeKind.Completed, null, null, handoff.RecordedAt);

        Assert.Equal(claim.HandlerId, start.StaffId);
        Assert.Equal(replacement, outcome.StaffId);
        Assert.Equal(handoff.Id, outcome.AssignmentId);
        Assert.Equal(start.Id, outcome.StartId);
        Assert.NotEqual(start.StaffId, outcome.StaffId);
    }

    [Fact]
    public void AttendedWithoutDrivingIsReasonedNotCompleted()
    {
        var booking = Booking();
        var assignment = Claim(booking);
        var outcome = Outcome(booking, assignment, null, TestDriveOutcomeKind.NotCompleted,
            " Licence unavailable ", null, booking.StartsAt);

        Assert.Null(outcome.StartId);
        Assert.True(outcome.CustomerPresent);
        Assert.Equal("Licence unavailable", outcome.Reason);
        Assert.Equal(TestDriveOutcomeKind.NotCompleted, outcome.Kind);
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null, TestDriveOutcomeKind.NotCompleted,
            null, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null, TestDriveOutcomeKind.Completed,
            null, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null, TestDriveOutcomeKind.NotCompleted,
            "Licence unavailable", null, AppointmentDay.EndsAt(booking.StartsAt)));
    }

    [Fact]
    public void OutcomeRejectsWrongStartChronologyAttendanceAndHandler()
    {
        var booking = Booking();
        var assignment = Claim(booking);
        var start = Start(booking, assignment);
        var other = Booking();
        var otherStart = Start(other, Claim(other));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, otherStart,
            TestDriveOutcomeKind.Completed, null, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, start,
            TestDriveOutcomeKind.Completed, null, null, start.StartedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => new TestDriveOutcome(Guid.NewGuid(), booking, assignment,
            assignment.HandlerId!.Value, start, TestDriveOutcomeKind.Completed, false, null, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => new TestDriveOutcome(Guid.NewGuid(), booking, assignment,
            Guid.NewGuid(), start, TestDriveOutcomeKind.Completed, true, null, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, start,
            TestDriveOutcomeKind.Completed, "Unexpected reason", null, booking.StartsAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Contains\ncontrol")]
    public void OutcomeRejectsEmptyOrControlText(string text)
    {
        var booking = Booking();
        var assignment = Claim(booking);
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null,
            TestDriveOutcomeKind.NotCompleted, text, null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null,
            TestDriveOutcomeKind.NotCompleted, "Licence unavailable", text, booking.StartsAt));
    }

    [Fact]
    public void OutcomeBoundsReasonAndNotesIndependently()
    {
        var booking = Booking();
        var assignment = Claim(booking);
        var text = new string('a', 1000);
        Assert.Equal(text, Outcome(booking, assignment, null, TestDriveOutcomeKind.NotCompleted,
            text, text, booking.StartsAt).Notes);
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null,
            TestDriveOutcomeKind.NotCompleted, text + "a", null, booking.StartsAt));
        Assert.Throws<ArgumentException>(() => Outcome(booking, assignment, null,
            TestDriveOutcomeKind.NotCompleted, "Licence unavailable", text + "a", booking.StartsAt));
    }

    private static TestDriveBooking Booking()
    {
        var startsAt = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new AppointmentSlot(startsAt, startsAt + TestDriveBooking.Duration), startsAt.AddDays(-2));
    }

    private static TestDriveAssignment Claim(TestDriveBooking booking)
    {
        var staffId = Guid.NewGuid();
        return new(Guid.NewGuid(), booking, staffId, TestDriveAssignmentKind.Claimed,
            staffId, null, null, booking.CreatedAt);
    }

    private static TestDriveStart Start(TestDriveBooking booking, TestDriveAssignment assignment, DateTimeOffset? now = null)
    {
        return new(Guid.NewGuid(), booking, assignment, assignment.HandlerId!.Value, true, true, now ?? booking.StartsAt);
    }

    private static TestDriveOutcome Outcome(TestDriveBooking booking, TestDriveAssignment assignment,
        TestDriveStart? start, string kind, string? reason, string? notes, DateTimeOffset endedAt)
    {
        return new(Guid.NewGuid(), booking, assignment, assignment.HandlerId!.Value, start, kind, true, reason, notes, endedAt);
    }
}
