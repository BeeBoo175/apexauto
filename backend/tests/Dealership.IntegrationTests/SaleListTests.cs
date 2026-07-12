using Dealership.Application.Sales;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.WorkCalendar;
using Microsoft.EntityFrameworkCore;

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
        Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.QueueAsync(world.Manager, "ToClaim", null, 1, 25,
            default)).Status);

        // With nothing awaiting approval the table opens on Open, and tab totals ignore the search.
        var table = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 25, null, mine.Buyer.Mobile, null, false), default)).Value!;
        Assert.Equal("Open", table.View);
        Assert.Equal((1, 1), (table.Counts.Open, table.Counts.BeingHandled));
        Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.TableAsync(world.Agent,
            new SaleTableQuery(1, 25, null, null, null, false), default)).Status);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        // The handling agent is found by their name as well as their email.
        var agentName = await database.StaffProfiles.Where(profile => profile.UserId == world.Agent)
            .Select(profile => profile.Name).SingleAsync();
        var byAgent = (await world.Lists.TableAsync(world.Manager,
            new SaleTableQuery(1, 25, "BeingHandled", agentName, null, false), default)).Value!;
        Assert.Contains(mine.Id, byAgent.Items.Select(item => item.Id));
        var calendar = new WorkCalendarStore(database);
        var query = UnscheduledWorkQuery.Parse(null, null, 1, 10)!;
        var work = (await calendar.UnscheduledAsync(world.Agent, query, default)).Value!;
        Assert.Equal(mine.Id, Assert.Single(work.Items).Id);
        Assert.Equal("Sale", work.Items[0].Kind);
        Assert.Equal("Handling", work.Items[0].Status);
        Assert.Equal(world.Agent, work.Items[0].HandlerId);
        Assert.Null(work.Items[0].InspectionId);
        Assert.DoesNotContain(work.Items, item => item.Id == open.Id);
        var colleague = (await calendar.UnscheduledAsync(world.Colleague, query, default)).Value!;
        Assert.Empty(colleague.Items);
        var otherBranch = (await calendar.UnscheduledAsync(world.OtherBranchAgent, query, default)).Value!;
        Assert.Empty(otherBranch.Items);
        Assert.Equal(WorkCalendarReadStatus.Forbidden, (await calendar.UnscheduledAsync(world.Agent,
            query with { Scope = new(null, "unassigned") }, default)).Status);
        var beyond = (await calendar.UnscheduledAsync(world.Agent, query with { Page = int.MaxValue }, default)).Value!;
        Assert.Equal(work.Total, beyond.Total);
        Assert.Empty(beyond.Items);
        var unassigned = query with { Scope = new(null, "unassigned") };
        Assert.Empty((await calendar.UnscheduledAsync(world.Manager, unassigned, default)).Value!.Items);
    }
}
