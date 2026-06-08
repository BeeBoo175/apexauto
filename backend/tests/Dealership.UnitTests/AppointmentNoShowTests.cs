using Dealership.Domain.Common;
using System.Globalization;
using Dealership.Domain.Intake;

namespace Dealership.UnitTests;

public sealed class AppointmentNoShowTests
{
    [Theory]
    [InlineData("2026-10-21T01:00:00Z", "2026-10-21T16:00:00Z")]
    [InlineData("2026-12-31T09:00:00Z", "2026-12-31T16:00:00Z")]
    [InlineData("2028-02-29T01:00:00Z", "2028-02-29T16:00:00Z")]
    public void NoShowStartsExactlyAtTheNextMalaysianMidnight(string start, string end)
    {
        var startsAt = Parse(start);
        var endsAt = Parse(end);
        Assert.Equal(endsAt, AppointmentDay.EndsAt(startsAt));
        Assert.Equal(AppointmentDay.StartUtc(startsAt), AppointmentDay.StartUtc(endsAt.AddTicks(-1)));
        Assert.Equal(endsAt, AppointmentDay.StartUtc(endsAt));
    }

    [Fact]
    public void CorrectionRetainsAttendanceAndRecordingTimesWithRequiredReason()
    {
        var start = Parse("2026-10-21T01:00:00Z");
        var appointment = new IntakeAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "VCA 6521",
            new AppointmentSlot(start, start.AddMinutes(90)), start.AddDays(-1),
            new SellerValuation("Proton", "Saga", "Standard", 2020, 90000, 18000, 23000, "MYR", 2026));
        var evidence = new CheckInEvidence("PL1BT65R84B123456", 90000, true, true, true, true, null);
        var midnight = AppointmentDay.EndsAt(start);
        var receipt = new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(), evidence,
            start, midnight, "  Arrival recorded late  ");
        Assert.Equal(start, receipt.CheckedInAt);
        Assert.Equal(midnight, receipt.CorrectionRecordedAt);
        Assert.Equal("Arrival recorded late", receipt.CorrectionReason);
        Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(),
            evidence, start, midnight.AddTicks(-1), "Too early"));
        Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(),
            evidence, midnight, midnight.AddDays(1), "Later date"));
        foreach (var reason in new[] { "", "   ", "Line\nbreak", new string('x', 1001) })
        {
            Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(),
                evidence, start, midnight, reason));
        }
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}
