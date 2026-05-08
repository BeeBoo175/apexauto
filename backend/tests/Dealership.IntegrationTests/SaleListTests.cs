using Dealership.Application.Sales;

namespace Dealership.IntegrationTests;

public sealed class SaleListTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task QueuesAndTablesReadTheSaleHeaderWithSearchAndTheFirstTabWithWork()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var open = await world.HoldAsync();
        var mine = await world.HoldAsync();
        await world.Handling.ClaimAsync(world.Agent, mine.Id, new(Guid.NewGuid(), 1), default);

        var queue = (await world.Lists.QueueAsync(world.Agent, "ToClaim", null, 1, 25, default)).Value!;
        Assert.Contains(open.Id, queue.Items.Select(item => item.Id));
        Assert.Equal(1, queue.Counts.Handling);
        // A search spans every tab and says where each match sits.
        var search = (await world.Lists.QueueAsync(world.Agent, "ToClaim", mine.Number, 1, 25, default)).Value!;
        Assert.Equal(("Handling", mine.Id), (Assert.Single(search.Items).Status, search.Items[0].Id));
        Assert.Equal(SaleStatus.Forbidden, (await world.Lists.QueueAsync(world.Manager, "ToClaim", null, 1, 25,
            default)).Status);

        // With nothing awaiting approval the table opens on Open, and tab totals ignore the search.
        var table = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 25, null, mine.Buyer.Mobile, null, false), default)).Value!;
        Assert.Equal("Open", table.View);
        Assert.Equal((1, 1), (table.Counts.Open, table.Counts.BeingHandled));
        Assert.Equal(SaleStatus.Forbidden, (await world.Lists.TableAsync(world.Agent,
            new SaleTableQuery(1, 25, null, null, null, false), default)).Status);
    }

    [Fact]
    public async Task ColumnFiltersNarrowTheManagerTable()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();
        var handling = new SaleTableQuery(1, 25, "BeingHandled", null, null, false);
        await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default);

        var byReference = (await world.Lists.TableAsync(world.Manager, handling with { Reference = held.Number },
            default)).Value!;
        Assert.Equal(held.Id, Assert.Single(byReference.Items).Id);
        var noPlate = (await world.Lists.TableAsync(world.Manager, handling with
        {
            Reference = held.Number,
            Registration = "NO-SUCH-PLATE"
        }, default)).Value!;
        Assert.Empty(noPlate.Items);
        // A sale that was never submitted has no excess, so it is outside any positive over-limit range.
        var overLimit = (await world.Lists.TableAsync(world.Manager, handling with
        {
            Reference = held.Number,
            MinimumOverLimit = 0.01m
        }, default)).Value!;
        Assert.Empty(overLimit.Items);
        // Close the sale so the shared branch's tab counts stay as the other tests expect.
        Assert.Equal(SaleStatus.Succeeded, (await world.Terms.CancelAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 2, "Filter test finished"), default)).Status);

        Assert.Null(SaleQueries.Table(1, 25, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, 10m, 5m, null, null));
        Assert.Null(SaleQueries.Table(1, 25, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, "2026-11-02", "2026-11-01"));
    }
}
