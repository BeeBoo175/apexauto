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
}
