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

    [Fact]
    public void WorkflowDurationReusesOpeningNoticeAndClosingWithoutChangingIntakeSlots()
    {
        var schedule = DefaultSchedule();
        var date = new DateOnly(2026, 10, 15);
        var now = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromMinutes(30);
        var slots = schedule.GetSlots(date, now, duration);

        Assert.Equal(18, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 1, 0, 0, TimeSpan.Zero), slots[0].StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero), slots[^1].EndsAt);
        Assert.All(slots, slot => Assert.Equal(duration, slot.EndsAt - slot.StartsAt));
        Assert.Equal(6, schedule.GetSlots(date, now).Count);
        Assert.Equal(slots[1], schedule.FindSlot(slots[1].StartsAt, now, duration));
        Assert.Null(schedule.FindSlot(slots[1].StartsAt, now));

        var cutoff = slots[^1].StartsAt.AddHours(-2);
        Assert.Equal(slots[^1], Assert.Single(schedule.GetSlots(date, cutoff, duration)));
        Assert.Empty(schedule.GetSlots(date, cutoff.AddTicks(1), duration));
    }

    [Fact]
    public void WorkflowDurationRetainsConfiguredDaysAndMalaysianHorizon()
    {
        var schedule = new InspectionSchedule(new TimeOnly(10, 0), new TimeOnly(11, 10), 60, 7, 0,
            [DayOfWeek.Thursday]);
        var now = new DateTimeOffset(2026, 10, 14, 16, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromMinutes(30);
        var date = new DateOnly(2026, 10, 15);
        var slots = schedule.GetSlots(date, now, duration);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 2, 0, 0, TimeSpan.Zero), slots[0].StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero), slots[^1].EndsAt);
        Assert.Empty(schedule.GetSlots(date.AddDays(1), now, duration));
        Assert.Empty(schedule.GetSlots(date.AddDays(7), now, duration));
        Assert.Empty(schedule.GetSlots(date.AddDays(-1), now, duration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(541)]
    public void InvalidWorkflowDurationIsRejected(int minutes)
    {
        var schedule = DefaultSchedule();
        var now = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.GetSlots(new DateOnly(2026, 10, 15),
            now, TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void SubminuteWorkflowDurationCannotCreateAnUnboundedSlotList()
    {
        var schedule = DefaultSchedule();
        var now = new DateTimeOffset(2026, 10, 14, 23, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.GetSlots(new DateOnly(2026, 10, 15),
            now, TimeSpan.FromTicks(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.FindSlot(now.AddHours(2), now,
            TimeSpan.FromSeconds(90)));
    }

    private static InspectionSchedule DefaultSchedule()
    {
        return new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0), 90, 30, 120, WorkingDays);
    }
}
