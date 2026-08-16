using Dealership.Application.Common;
using Dealership.Application.Sales;

namespace Dealership.UnitTests;

public sealed class SaleQueriesTests
{
    [Fact]
    public void QueueAndManagerStagesHaveDistinctValidatedViews()
    {
        foreach (var view in new[] { SaleViews.Settling, SaleViews.Completed })
        {
            Assert.True(SaleQueries.ValidQueue(view, null, 1, 10));
            Assert.Null(SaleQueries.Table(new() { View = view }));
        }

        foreach (var view in new[] { SaleViews.Approved, SaleViews.AwaitingHandover, SaleViews.Delivered })
        {
            Assert.NotNull(SaleQueries.Table(new() { View = char.ToLowerInvariant(view[0]) + view[1..] }));
            Assert.False(SaleQueries.ValidQueue(view, null, 1, 10));
        }

        Assert.False(SaleQueries.ValidQueue(SaleViews.Completed, null, 0, 10));
        Assert.False(SaleQueries.ValidQueue(SaleViews.Settling, null, 1, 20));
        Assert.False(SaleQueries.ValidQueue("Settled", null, 1, 10));
        Assert.Null(SaleQueries.Table(new() { View = "Settled" }));
    }

    [Fact]
    public void NamedFiltersKeepTheirValuesAndNormalizeOnlySearchText()
    {
        var query = SaleQueries.Table(new()
        {
            Page = 2,
            PageSize = 50,
            View = "history",
            Search = "  buyer@example.invalid  ",
            Sort = "updated",
            Direction = "desc",
            Handler = "  agent@example.invalid  ",
            Branch = "  Glenmarie  ",
            Outcome = "cancelled",
            MinimumDiscount = 1.01m,
            MaximumDiscount = 2.02m,
            MinimumBalance = 3.03m,
            MaximumBalance = 4.04m,
            Registration = "  WXY1234  ",
            Vehicle = "  Proton Saga  ",
            Customer = "  Nur Aina  ",
            Reference = "  VRS-2026-123456  ",
            MinimumHoursLeft = 5.5m,
            MaximumHoursLeft = 6.5m,
            UpdatedFrom = "2026-11-01",
            UpdatedTo = "2026-11-02"
        });

        Assert.NotNull(query);
        Assert.Equal((2, 50, "History", "updated", true),
            (query.Page, query.PageSize, query.View, query.Sort, query.Descending));
        Assert.Equal("buyer@example.invalid", query.Search);
        Assert.Equal("agent@example.invalid", query.Agent);
        Assert.Equal("Glenmarie", query.Branch);
        Assert.Equal("Cancelled", query.Outcome);
        Assert.Equal("WXY1234", query.Registration);
        Assert.Equal("Proton Saga", query.Vehicle);
        Assert.Equal("Nur Aina", query.Customer);
        Assert.Equal("VRS-2026-123456", query.Reference);
        Assert.Equal((1.01m, 2.02m, 3.03m, 4.04m),
            (query.MinimumDiscount, query.MaximumDiscount, query.MinimumBalance, query.MaximumBalance));
        Assert.Equal((5.5m, 6.5m), (query.MinimumHoursLeft, query.MaximumHoursLeft));
        Assert.Equal(ListDateRange.Parse("2026-11-01", "2026-11-02"), query.Updated);
    }

    [Fact]
    public void OmittedAndBlankFiltersKeepTheDefaultUnfilteredQuery()
    {
        var expected = new SaleTableQuery(1, Pagination.DefaultPageSize, null, null, null, false);
        Assert.Equal(expected, SaleQueries.Table(new()));
        Assert.Equal(expected, SaleQueries.Table(new()
        {
            Search = "  ",
            Handler = "",
            Branch = "\t",
            Registration = "\n",
            Vehicle = "\r\n",
            Customer = "\u00a0",
            Reference = " "
        }));
    }

    [Theory]
    [InlineData(160, false, true)]
    [InlineData(161, false, false)]
    [InlineData(3, true, false)]
    public void EveryTextFilterUsesTheSameTrimmedSearchBounds(int length, bool control, bool accepted)
    {
        var text = control ? "a\tb" : new string('a', length);
        var padded = $"  {text}  ";
        SaleTableInput[] inputs =
        [
            new() { Search = padded },
            new() { Handler = padded },
            new() { Branch = padded },
            new() { Registration = padded },
            new() { Vehicle = padded },
            new() { Customer = padded },
            new() { Reference = padded }
        ];
        foreach (var input in inputs)
        {
            Assert.Equal(accepted, SaleQueries.Table(input) is not null);
        }
    }

    [Fact]
    public void AllSalesIsAcceptedAsATableView()
    {
        var query = SaleQueries.Table(new() { View = "all" });

        Assert.NotNull(query);
        Assert.Equal(SaleViews.All, query.View);
    }

    [Fact]
    public void InvalidCriteriaAreRefusedBeforeReading()
    {
        SaleTableInput[] inputs =
        [
            new() { Page = 0 },
            new() { PageSize = 20 },
            new() { View = " History " },
            new() { Sort = "claimed" },
            new() { Direction = "asc" },
            new() { Sort = "balance", Direction = "DESC" },
            new() { Outcome = "Open" },
            new() { MinimumDiscount = 2m, MaximumDiscount = 1m },
            new() { MinimumBalance = 2m, MaximumBalance = 1m },
            new() { MinimumHoursLeft = -1m },
            new() { MaximumHoursLeft = 169m },
            new() { MinimumHoursLeft = 2m, MaximumHoursLeft = 1m },
            new() { UpdatedFrom = "2026-11-01" },
            new() { UpdatedFrom = "2026-11-02", UpdatedTo = "2026-11-01" }
        ];
        foreach (var input in inputs)
        {
            Assert.Null(SaleQueries.Table(input));
        }
    }
}
