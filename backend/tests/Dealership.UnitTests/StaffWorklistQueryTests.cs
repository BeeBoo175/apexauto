using Dealership.Application.Intake;

namespace Dealership.UnitTests;

public sealed class StaffWorklistQueryTests
{
    private static readonly DateTimeOffset BeforeMidnight = new(2026, 10, 15, 15, 59, 59, TimeSpan.Zero);

    [Fact]
    public void DefaultTodayUsesMalaysianMidnightAndExplicitInclusiveDatesAreBounded()
    {
        var today = Parse(now: BeforeMidnight)!;
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 16, 0, 0, TimeSpan.Zero), today.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 16, 0, 0, TimeSpan.Zero), today.Until);
        var tomorrow = Parse(now: BeforeMidnight.AddSeconds(1))!;
        Assert.Equal(today.Until, tomorrow.From);
        Assert.Equal(StaffWorklistStatus.Booked, tomorrow.Status);
        Assert.Equal(20, tomorrow.PageSize);
        var explicitDay = Parse("2026-10-16", "2026-10-16")!;
        Assert.Equal(tomorrow.From, explicitDay.From);
        Assert.Equal(tomorrow.Until, explicitDay.Until);
        Assert.NotNull(Parse("2024-01-01", "2024-12-31", pageSize: 50));
        Assert.Null(Parse("2024-01-01", "2025-01-01"));
        Assert.NotNull(Parse(page: int.MaxValue));

        foreach (var state in new[] { "Booked", "CheckedIn", "Cancelled", "All" })
        {
            Assert.Equal(state, Parse(status: state)!.Status.ToString());
        }
    }

    [Theory]
    [InlineData("2026-10-16", null, null, 1, 20)]
    [InlineData(null, "2026-10-16", null, 1, 20)]
    [InlineData("", "", null, 1, 20)]
    [InlineData("16/10/2026", "2026-10-16", null, 1, 20)]
    [InlineData("2026-10-17", "2026-10-16", null, 1, 20)]
    [InlineData("9999-12-31", "9999-12-31", null, 1, 20)]
    [InlineData("0001-01-01", "0001-01-01", null, 1, 20)]
    [InlineData(null, null, "booked", 1, 20)]
    [InlineData(null, null, "0", 1, 20)]
    [InlineData(null, null, "", 1, 20)]
    [InlineData(null, null, null, 0, 20)]
    [InlineData(null, null, null, 1, 0)]
    [InlineData(null, null, null, 1, 51)]
    public void InvalidFiltersAreRejected(string? from, string? to, string? status, int page, int pageSize)
    {
        Assert.Null(Parse(from, to, status, page, pageSize));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, Guid.Empty, 1, 20, BeforeMidnight));
    }

    private static StaffWorklistQuery? Parse(string? from = null, string? to = null, string? status = null,
        int page = 1, int pageSize = 20, DateTimeOffset? now = null)
    {
        return StaffWorklistQuery.Parse(from, to, status, null, page, pageSize, now ?? BeforeMidnight);
    }
}
