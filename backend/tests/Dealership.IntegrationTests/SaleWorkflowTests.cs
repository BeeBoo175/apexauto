using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class SaleWorkflowTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OneSaleCarriesAHoldFromConfirmationThroughClaimReleaseAndReclaim()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();

        await using (var database = NewContext())
        {
            var opened = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.Equal((SalesOrderStatus.Open, held.ExpiresAt, 1L), (opened.Status, opened.DueAt, opened.Version));
            var created = await database.SalesOrderEvents.AsNoTracking().SingleAsync(item => item.SalesOrderId == held.Id);
            Assert.Equal((SalesOrderEventKind.Created, held.CustomerId), (created.Kind, created.ActorId));
        }

        // Only an agent of the sale's branch claims; a replay returns the same receipt and a second claim conflicts.
        Assert.Equal(SaleStatus.Forbidden, (await world.Handling.ClaimAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleStatus.NotFound, (await world.Handling.ClaimAsync(world.OtherBranchAgent, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        var claim = new SaleCommandRequest(Guid.NewGuid(), 1);
        var claimed = await world.Handling.ClaimAsync(world.Agent, held.Id, claim, default);
        Assert.Equal(SaleStatus.Succeeded, claimed.Status);
        Assert.Equal(claimed.Receipt, (await world.Handling.ClaimAsync(world.Agent, held.Id, claim, default)).Receipt);
        // Once handled, the sale is invisible to other agents, so a late claim learns nothing about it.
        Assert.Equal(SaleStatus.NotFound, (await world.Handling.ClaimAsync(world.Colleague, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleStatus.Conflict, (await world.Handling.ClaimAsync(world.Agent, held.Id,
            new(claim.OperationId, 2), default)).Status);

        // The handling agent now reads the customer's contact; the colleague no longer sees the sale at all.
        var view = (await world.Reads.GetAsync(world.Agent, held.Id, default)).Value!;
        Assert.Equal((SalesOrderStatus.Handling, null, held.Buyer.Mobile), (view.Sale.Status, view.Sale.DueAt,
            view.Context.CustomerMobile));
        Assert.True(view.Actions.SaveTerms && view.Actions.Submit && view.Actions.Release && !view.Actions.Decide);
        Assert.Equal(SaleStatus.NotFound, (await world.Reads.GetAsync(world.Colleague, held.Id, default)).Status);

        var terms = await world.Terms.SaveTermsAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 2, null, 300m, null, 0m), default);
        Assert.Equal((SaleStatus.Succeeded, 3L), (terms.Status, terms.Receipt!.Version));

        var released = await world.Handling.ReleaseAsync(world.Agent, held.Id, new(Guid.NewGuid(), 3), default);
        Assert.Equal(SaleStatus.Succeeded, released.Status);
        var reclaimed = await world.Handling.AssignAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, world.Colleague), default);
        Assert.Equal(("Assigned", 5L), (reclaimed.Receipt!.Kind, reclaimed.Receipt.Version));

        await using (var database = NewContext())
        {
            // Release and reclaim reuse the one sale; release reset the terms and resumed the remaining time.
            Assert.Equal(1, await database.SalesOrders.CountAsync(item => item.Id == held.Id));
            var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.Equal((world.Colleague, 0m, null), (sale.AgentId, sale.NegotiatedDiscount, sale.DueAt));
        }

        // Every entry, newest first, can be opened as the sale stood after it, beside the version before it.
        var history = (await world.Reads.HistoryAsync(world.Manager, held.Id, 1, 10, default)).Value!;
        Assert.Equal(["Assigned", "Released", "TermsSaved", "Claimed", "Created"], history.Items.Select(item => item.Kind));
        Assert.Contains(history.Items[1].Changes,
            change => change is { Field: SaleChange.NegotiatedDiscount, Before: "300.00" });
        for (var version = 1; version <= 5; version++)
        {
            var past = (await world.Reads.VersionAsync(world.Manager, held.Id, version, default)).Value!;
            Assert.Equal(version, past.Snapshot.Version);
            Assert.Equal(version == 1, past.Previous is null);
        }

        Assert.Equal(300m,
            (await world.Reads.VersionAsync(world.Manager, held.Id, 3, default)).Value!.Snapshot.NegotiatedDiscount);
        Assert.Equal(SaleStatus.NotFound, (await world.Reads.HistoryAsync(world.Agent, held.Id, 1, 10, default)).Status);
    }

    [Fact]
    public async Task TheDatabaseKeepsSalesAndTheirHistoryIntact()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var held = await world.HoldAsync();
        await using var database = NewContext();

        foreach (var statement in new[]
        {
            $"DELETE FROM \"SalesOrders\" WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrders\" SET \"BasePrice\" = 1, \"Version\" = 2 WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrders\" SET \"NegotiatedDiscount\" = 0 WHERE \"Id\" = '{held.Id}'",
            $"UPDATE \"SalesOrderEvents\" SET \"Reason\" = 'x' WHERE \"SalesOrderId\" = '{held.Id}'"
        })
        {
            await Assert.ThrowsAnyAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(statement));
        }

        // A change without its history entry is refused at commit.
        await using var transaction = await database.Database.BeginTransactionAsync();
        await database.Database.ExecuteSqlRawAsync(
            $"UPDATE \"SalesOrders\" SET \"Version\" = 2, \"UpdatedAt\" = clock_timestamp() WHERE \"Id\" = '{held.Id}'");
        await Assert.ThrowsAnyAsync<PostgresException>(() => transaction.CommitAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
