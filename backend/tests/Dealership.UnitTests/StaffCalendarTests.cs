using Dealership.Application.Intake;

namespace Dealership.UnitTests;

public sealed class StaffCalendarTests
{
    [Fact]
    public void PeriodUsesMalaysianDaysAndAcceptsExactlySixWeeks()
    {
        var now = new DateTimeOffset(2026, 10, 19, 16, 0, 0, TimeSpan.Zero);
        Assert.Null(StaffCalendarService.Parse(null, null, null, null));
        var today = StaffCalendarService.Parse("2026-10-20", "2026-10-20", null, null)!;
        Assert.Equal(now, today.From);
        Assert.Equal(now.AddDays(1), today.Until);
        Assert.Equal(StaffWorklistStatus.All, today.Status);
        var month = StaffCalendarService.Parse("2026-09-28", "2026-11-08", "All", null)!;
        Assert.Equal(TimeSpan.FromDays(42), month.Until - month.From);
        Assert.Null(StaffCalendarService.Parse("2026-09-28", "2026-11-09", "All", null));
        Assert.Null(StaffCalendarService.Parse("2026-10-20", null, null, null));
        Assert.Null(StaffCalendarService.Parse("2026-10-20", "2026-10-19", null, null));
        Assert.Null(StaffCalendarService.Parse("2026-10-20", "2026-10-20", "Unknown", null));
    }

    [Theory]
    [InlineData(-1, StaffCalendarStatus.Forbidden)]
    [InlineData(0, StaffCalendarStatus.Succeeded)]
    [InlineData(1000, StaffCalendarStatus.Succeeded)]
    [InlineData(1001, StaffCalendarStatus.TooMany)]
    public async Task CompleteRangeIsAuthorizedAndRefusedRatherThanTruncated(int total, StaffCalendarStatus expected)
    {
        var startsAt = new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
        var rows = Enumerable.Range(0, Math.Max(0, total)).Select(_ => new StaffWorklistItem(
            Guid.NewGuid(), Guid.NewGuid(), "ApexAuto Glenmarie", "Nur Aina Abdullah", "VCA 9701",
            startsAt, startsAt.AddMinutes(90), "Booked")).ToArray();
        var store = new CalendarStore(total < 0 ? null : new StaffWorklistPage(rows, total, 1, 1000));
        var service = new StaffCalendarService(store);
        var query = StaffCalendarService.Parse("2026-09-28", "2026-11-08", null, Guid.NewGuid())!;
        var actor = Guid.NewGuid();
        var result = await service.ReadAsync(actor, query, CancellationToken.None);
        Assert.Equal(expected, result.Status);
        Assert.Equal(actor, store.Actor);
        Assert.Same(query, store.Query);
        if (expected != StaffCalendarStatus.Succeeded)
        {
            Assert.Null(result.Snapshot);
            return;
        }
        Assert.Equal(total, result.Snapshot!.Items.Count);
        Assert.Equal(new DateOnly(2026, 9, 28), result.Snapshot.From);
        Assert.Equal(new DateOnly(2026, 11, 8), result.Snapshot.To);
    }

    private sealed class CalendarStore(StaffWorklistPage? result) : IStaffWorklistStore
    {
        public Guid Actor { get; private set; }
        public StaffWorklistQuery? Query { get; private set; }

        public Task<StaffWorklistPage?> CalendarAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
        {
            Actor = staffId;
            Query = query;
            return Task.FromResult(result);
        }

        public Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Calendar must not page the list.");

        public Task<StaffWorklistPage?> ExportAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Calendar must not invoke CSV export.");
    }
}
