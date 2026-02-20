using Dealership.Application.Inspections;

namespace Dealership.UnitTests;

public sealed class InspectionQueueFilterTests
{
    [Fact]
    public void DatesCoverInclusiveMalaysianDaysAndOmissionMeansAll()
    {
        var all = InspectionQueueFilter.Parse(null, null, null)!;
        Assert.Null(all.From);
        Assert.Null(all.Until);
        Assert.Equal(25, all.PageSize);
        var day = InspectionQueueFilter.Parse("2026-10-22", "2026-10-22", null, search: "  myvi  ")!;
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 16, 0, 0, TimeSpan.Zero), day.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 16, 0, 0, TimeSpan.Zero), day.Until);
        Assert.Equal("myvi", day.Search);
        Assert.NotNull(InspectionQueueFilter.Parse("2026-01-01", "2027-01-01", null));
        Assert.Null(InspectionQueueFilter.Parse("2026-01-01", "2027-01-02", null));
    }

    [Theory]
    [InlineData("2026-10-22", null)]
    [InlineData(null, "2026-10-22")]
    [InlineData("2026-10-23", "2026-10-22")]
    [InlineData("2026-02-30", "2026-03-01")]
    [InlineData("0001-01-01", "0001-01-01")]
    [InlineData("9999-12-31", "9999-12-31")]
    public void InvalidDatePairsAreRejected(string? from, string? to)
    {
        Assert.Null(InspectionQueueFilter.Parse(from, to, null));
    }

    [Fact]
    public void PagingSearchAndDirectStoreInputsAreBounded()
    {
        Assert.Null(InspectionQueueFilter.Parse(null, null, Guid.Empty));
        Assert.Null(InspectionQueueFilter.Parse(null, null, null, page: 0));
        Assert.Null(InspectionQueueFilter.Parse(null, null, null, pageSize: 20));
        Assert.Null(InspectionQueueFilter.Parse(null, null, null, search: "myvi\nplate"));
        Assert.Null(InspectionQueueFilter.Parse(null, null, null, search: new string('a', 161)));
        Assert.False(new InspectionQueueFilter(From: DateTimeOffset.UtcNow).IsValid);
        Assert.False(new InspectionQueueFilter(Search: null!).IsValid);
        foreach (var size in new[] { 10, 25, 50 })
        {
            Assert.NotNull(InspectionQueueFilter.Parse(null, null, null, int.MaxValue, size));
        }
    }
}
