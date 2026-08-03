using Dealership.Application.Intake;

namespace Dealership.UnitTests;

public sealed class StaffWorklistQueryTests
{
    [Fact]
    public void OmittedDatesAreUnrestrictedAndExplicitInclusiveDatesUseMalaysianMidnight()
    {
        var unrestricted = Parse()!;
        Assert.Null(unrestricted.From);
        Assert.Null(unrestricted.Until);
        var today = Parse("2026-10-15", "2026-10-15")!;
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 16, 0, 0, TimeSpan.Zero), today.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 16, 0, 0, TimeSpan.Zero), today.Until);
        var tomorrow = Parse("2026-10-16", "2026-10-16")!;
        Assert.Equal(today.Until, tomorrow.From);
        Assert.Equal(StaffWorklistStatus.Booked, tomorrow.Status);
        Assert.Equal(25, tomorrow.PageSize);
        var explicitDay = Parse("2026-10-16", "2026-10-16")!;
        Assert.Equal(tomorrow.From, explicitDay.From);
        Assert.Equal(tomorrow.Until, explicitDay.Until);
        Assert.NotNull(Parse("2024-01-01", "2024-12-31", pageSize: 50));
        Assert.Null(Parse("2024-01-01", "2025-01-01"));
        Assert.NotNull(Parse(page: int.MaxValue));

        foreach (var (token, expected) in new (string Token, StaffWorklistStatus Expected)[]
        {
            ("booked", StaffWorklistStatus.Booked),
            ("checkedIn", StaffWorklistStatus.CheckedIn),
            ("cancelled", StaffWorklistStatus.Cancelled),
            ("noShow", StaffWorklistStatus.NoShow),
            ("all", StaffWorklistStatus.All)
        })
        {
            Assert.Equal(expected, Parse(status: token)!.Status);
        }

        foreach (var queue in new[] { "toClaim", "myWork", "history" })
        {
            Assert.Equal(queue, StaffWorklistQuery.Parse(null, null, "all", null, 1, 25, queue: queue)!.Queue);
        }
    }

    [Theory]
    [InlineData("2026-10-16", null, null, 1, 25)]
    [InlineData(null, "2026-10-16", null, 1, 25)]
    [InlineData("", "", null, 1, 25)]
    [InlineData("16/10/2026", "2026-10-16", null, 1, 25)]
    [InlineData("2026-10-17", "2026-10-16", null, 1, 25)]
    [InlineData("9999-12-31", "9999-12-31", null, 1, 25)]
    [InlineData("0001-01-01", "0001-01-01", null, 1, 25)]
    [InlineData(null, null, "Booked", 1, 25)]
    [InlineData(null, null, "CheckedIn", 1, 25)]
    [InlineData(null, null, "Cancelled", 1, 25)]
    [InlineData(null, null, "NoShow", 1, 25)]
    [InlineData(null, null, "All", 1, 25)]
    [InlineData(null, null, "0", 1, 25)]
    [InlineData(null, null, "", 1, 25)]
    [InlineData(null, null, null, 0, 25)]
    [InlineData(null, null, null, 1, 0)]
    [InlineData(null, null, null, 1, 51)]
    public void InvalidFiltersAreRejected(string? from, string? to, string? status, int page, int pageSize)
    {
        Assert.Null(Parse(from, to, status, page, pageSize));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, Guid.Empty, 1, 25));
        Assert.Null(StaffWorklistQuery.Parse(null, null, "all", null, 1, 25, queue: "MyWork"));
        Assert.Null(StaffWorklistQuery.Parse(null, null, "all", null, 1, 25, queue: "ToClaim"));
        Assert.Null(StaffWorklistQuery.Parse(null, null, "all", null, 1, 25, queue: "History"));
    }

    private static StaffWorklistQuery? Parse(string? from = null, string? to = null, string? status = null,
        int page = 1, int pageSize = 25)
    {
        return StaffWorklistQuery.Parse(from, to, status, null, page, pageSize);
    }

    [Fact]
    public void SearchAndSortAreBoundedAndExplicit()
    {
        var query = StaffWorklistQuery.Parse(null, null, null, null, 1, 25,
            "  VCA 6242  ", "registrationPlate", "desc")!;
        Assert.Equal("VCA 6242", query.Search);
        Assert.Equal("registrationPlate", query.Sort);
        Assert.Equal("desc", query.Direction);
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, new string('a', 161)));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, "a\nb"));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, sort: "arbitrary"));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, direction: "DESC"));
        var handler = StaffWorklistQuery.Parse(null, null, "all", null, 1, 25,
            sort: "handlerEmail", handler: "  aina.sofia  ")!;
        Assert.Equal("aina.sofia", handler.Handler);
        Assert.Equal("handlerEmail", handler.Sort);
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, handler: new string('a', 161)));
        Assert.Null(StaffWorklistQuery.Parse(null, null, null, null, 1, 25, handler: "a\nb"));
    }
}
