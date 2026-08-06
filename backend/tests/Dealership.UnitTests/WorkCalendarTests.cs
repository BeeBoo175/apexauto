using Dealership.Application.WorkCalendar;

namespace Dealership.UnitTests;

public sealed class WorkCalendarTests
{
    [Fact]
    public void PeriodUsesMalaysianDaysAndAcceptsExactlySixWeeks()
    {
        var midnight = new DateTimeOffset(2026, 10, 19, 16, 0, 0, TimeSpan.Zero);
        Assert.Null(WorkCalendarQuery.Parse(null, null, null, null));
        var today = WorkCalendarQuery.Parse("2026-10-20", "2026-10-20", null, null)!;
        Assert.Equal(midnight, today.Dates.From);
        Assert.Equal(midnight.AddDays(1), today.Dates.Until);
        var month = WorkCalendarQuery.Parse("2026-09-28", "2026-11-08", null, null)!;
        Assert.Equal(TimeSpan.FromDays(42), month.Dates.Until - month.Dates.From);
        Assert.Null(WorkCalendarQuery.Parse("2026-09-28", "2026-11-09", null, null));
        Assert.Null(WorkCalendarQuery.Parse("2026-10-20", null, null, null));
        Assert.Null(WorkCalendarQuery.Parse("2026-10-20", "2026-10-19", null, null));
        Assert.Null(WorkCalendarQuery.Parse("2026-10-20", "2026-10-20", Guid.Empty, null));
        Assert.Equal("aina", WorkCalendarQuery.Parse("2026-10-20", "2026-10-20", null, " aina ")!.Scope.Employee);
    }

    [Fact]
    public void SharedSearchAndPanelFiltersRejectInvalidInputs()
    {
        var query = WorkCalendarQuery.Parse("2026-11-01", "2026-11-30", null, null,
            " aina.sofia ", "unassigned")!;
        Assert.Equal("aina.sofia", query.Search);
        Assert.Equal("Unassigned", query.Assignment);
        Assert.Null(query.Scope.BranchId);
        Assert.Null(WorkCalendarQuery.Parse("2026-11-01", "2026-11-30", null, null,
            assignment: "unknown"));
        Assert.Null(WorkCalendarQuery.Parse("2026-11-01", "2026-11-30", null, null,
            new string('a', 161)));

        var work = UnscheduledWorkQuery.Parse(null, null, 2, 10, " Proton ", "workshop")!;
        Assert.Equal("Proton", work.Search);
        Assert.Equal("Workshop", work.Kind);
        Assert.Equal(2, work.Page);
        Assert.Null(UnscheduledWorkQuery.Parse(null, null, 1, 5));
        Assert.Null(UnscheduledWorkQuery.Parse(null, null, 1, 25, kind: "intake"));
    }
}
