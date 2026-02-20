using Dealership.Application.Intake;

namespace Dealership.UnitTests;

public sealed class AppointmentHistoryQueryTests
{
    [Fact]
    public void MalaysianCalendarDayBecomesHalfOpenUtcRange()
    {
        var query = AppointmentHistoryQuery.Parse("2026-10-16", "2026-10-16", 1, 25)!;
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 16, 0, 0, TimeSpan.Zero), query.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 16, 0, 0, TimeSpan.Zero), query.Until);
        Assert.NotNull(AppointmentHistoryQuery.Parse("2024-01-01", "2024-12-31", 1, 50));
        Assert.Null(AppointmentHistoryQuery.Parse("2024-01-01", "2025-01-01", 1, 25));
        Assert.NotNull(AppointmentHistoryQuery.Parse(null, null, 1, 25));
    }

    [Theory]
    [InlineData("2026-10-16", null, 1, 25)]
    [InlineData(null, "2026-10-16", 1, 25)]
    [InlineData("", "", 1, 25)]
    [InlineData("16/10/2026", "2026-10-16", 1, 25)]
    [InlineData("2026-10-17", "2026-10-16", 1, 25)]
    [InlineData("9999-12-31", "9999-12-31", 1, 25)]
    [InlineData(null, null, 0, 25)]
    [InlineData(null, null, 1, 51)]
    public void InvalidFiltersAreRejected(string? from, string? to, int page, int size)
    {
        Assert.Null(AppointmentHistoryQuery.Parse(from, to, page, size));
    }
}
