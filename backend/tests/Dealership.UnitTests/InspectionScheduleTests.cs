using Dealership.Domain.Intake;

namespace Dealership.UnitTests;

public sealed class InspectionScheduleTests
{
    private static readonly DayOfWeek[] WorkingDays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
    ];

    [Fact]
    public void CompleteSlotsEndAtClosingAndExactNoticeIsIncluded()
    {
        var schedule = DefaultSchedule();
        var date = new DateOnly(2026, 10, 15);
        var morning = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);
        var slots = schedule.GetSlots(date, morning);

        Assert.Equal(6, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 1, 0, 0, TimeSpan.Zero), slots[0].StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero), slots[^1].EndsAt);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(90), slot.EndsAt - slot.StartsAt));
        var exactCutoff = new DateTimeOffset(2026, 10, 15, 6, 30, 0, TimeSpan.Zero);
        Assert.Equal(slots[^1], Assert.Single(schedule.GetSlots(date, exactCutoff)));
        Assert.Empty(schedule.GetSlots(date, exactCutoff.AddTicks(1)));
    }

    [Fact]
    public void MalaysianMidnightControlsHorizonAndClosedDaysAreExcluded()
    {
        var schedule = DefaultSchedule();
        var beforeMidnight = new DateTimeOffset(2026, 10, 15, 15, 59, 59, TimeSpan.Zero);
        var window = schedule.GetDateWindow(beforeMidnight);

        Assert.Equal(new DateOnly(2026, 10, 15), window.FirstDate);
        Assert.Equal(new DateOnly(2026, 11, 13), window.LastDate);
        Assert.Equal(new DateOnly(2026, 10, 16), schedule.GetDateWindow(beforeMidnight.AddSeconds(1)).FirstDate);
        Assert.NotEmpty(schedule.GetSlots(window.LastDate, beforeMidnight));
        Assert.Empty(schedule.GetSlots(window.LastDate.AddDays(1), beforeMidnight));
        Assert.Empty(schedule.GetSlots(window.FirstDate.AddDays(-1), beforeMidnight));
        Assert.NotEmpty(schedule.GetSlots(new DateOnly(2026, 10, 17), beforeMidnight));
        Assert.Empty(schedule.GetSlots(new DateOnly(2026, 10, 18), beforeMidnight));
    }

    [Fact]
    public void ConfigurationChangesDurationAndNeverCreatesPartialClosingSlot()
    {
        var schedule = new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0), 100, 7, 0, WorkingDays);
        var now = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);
        var slots = schedule.GetSlots(new DateOnly(2026, 10, 15), now);

        Assert.Equal(5, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 9, 20, 0, TimeSpan.Zero), slots[^1].EndsAt);
        Assert.Equal(new DateOnly(2026, 10, 21), schedule.GetDateWindow(now).LastDate);
    }

    [Theory]
    [InlineData(0, 30, 120)]
    [InlineData(600, 30, 120)]
    [InlineData(90, 0, 120)]
    [InlineData(90, 367, 120)]
    [InlineData(90, 30, -1)]
    public void InvalidPolicyFailsBeforeUse(int duration, int horizon, int notice)
    {
        Assert.Throws<ArgumentException>(() => new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0),
            duration, horizon, notice, WorkingDays));
    }

    private static InspectionSchedule DefaultSchedule()
    {
        return new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0), 90, 30, 120, WorkingDays);
    }
}
