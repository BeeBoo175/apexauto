using System.Globalization;
using Dealership.Application.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

public sealed class DevelopmentIntakeTimelineTests
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
    private static readonly InspectionSchedule Schedule = new(new TimeOnly(9, 0), new TimeOnly(18, 0),
        90, 30, 120, [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]);

    [Theory]
    [InlineData("2026-10-18T00:00:00+08:00")]
    [InlineData("2026-10-19T18:01:00+08:00")]
    [InlineData("2026-10-19T23:59:59.9999999+08:00")]
    [InlineData("2026-10-20T00:00:00+08:00")]
    [InlineData("2026-12-31T23:59:59.9999999+08:00")]
    [InlineData("2027-01-01T00:00:00+08:00")]
    public void SnapshotDatesObeyBusinessScheduleAndHistoricalTransitions(string date)
    {
        var anchor = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture);
        var timeline = DevelopmentIntakeTimeline.Create(Schedule, anchor);
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count, timeline.Count);
        foreach (var branch in timeline.GroupBy(item => item.Fixture.BranchId))
        {
            Assert.Equal(DevelopmentIntakeManifest.TotalPerBranch, branch.Select(item => item.Slot.StartsAt).Distinct().Count());
            Assert.Equal(41, branch.Count(item => item.Fixture.Status == StaffWorklistStatus.Booked));
        }

        foreach (var moment in timeline)
        {
            Assert.Equal(moment.Slot, Schedule.FindSlot(moment.Slot.StartsAt, moment.BookedAt));
            Assert.NotEqual(DayOfWeek.Sunday, TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, Malaysia).DayOfWeek);
            if (moment.Fixture.Status == StaffWorklistStatus.Booked)
            {
                Assert.True(moment.Slot.StartsAt > anchor);
                Assert.Equal(anchor, moment.BookedAt);
                Assert.Null(moment.TransitionAt);
                continue;
            }

            Assert.True(moment.Slot.EndsAt < anchor);
            Assert.True(moment.TransitionAt >= moment.BookedAt);
            Assert.True(moment.TransitionAt <= anchor);
            if (moment.Fixture.Status == StaffWorklistStatus.Cancelled)
            {
                Assert.True(moment.TransitionAt < moment.Slot.StartsAt);
                continue;
            }

            if (moment.Fixture.Status == StaffWorklistStatus.NoShow)
            {
                Assert.Equal(AppointmentDay.EndsAt(moment.Slot.StartsAt), moment.TransitionAt);
                continue;
            }

            Assert.Equal(TimeZoneInfo.ConvertTime(moment.Slot.StartsAt, Malaysia).Date,
                TimeZoneInfo.ConvertTime(moment.TransitionAt!.Value, Malaysia).Date);
        }
    }

    [Fact]
    public void InsufficientConfiguredCapacityFailsBeforeInitialization()
    {
        var schedule = new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
            90, 1, 120, [DayOfWeek.Monday]);
        Assert.Throws<InvalidOperationException>(() => DevelopmentIntakeTimeline.Create(schedule,
            DateTimeOffset.Parse("2026-10-18T12:00:00+08:00", CultureInfo.InvariantCulture)));
    }
}
