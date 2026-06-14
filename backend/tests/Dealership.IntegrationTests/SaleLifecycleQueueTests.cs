using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class SaleLifecycleQueueTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SettlementAndDeliveryStaySearchableWithScopedCountsAndStableFullFullPartialPages()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var holds = new List<Reservation>();
        for (var index = 0; index < 21; index++)
        {
            var held = await ApprovedAsync(world);
            holds.Add(held);
            if (index % 2 == 0)
            {
                await SettleAsync(world, held);
            }
        }

        var queue = (await world.Lists.QueueAsync(world.Agent, SaleViews.Settling, null, 1, 10, default)).Value!;
        Assert.Equal((21, 21, 0), (queue.Total, queue.Counts.Settling, queue.Counts.Completed));
        var allMatches = (await world.Lists.QueueAsync(world.Agent, SaleViews.ToClaim, "Hafiz", 1, 10, default)).Value!;
        Assert.Equal((21, 21), (allMatches.Total, allMatches.Counts.Settling));
        await VerifyPagesAsync(world, SaleViews.Settling, [.. holds.Select(item => item.Id)]);
        var approved = await TableAsync(world, SaleViews.Approved);
        var awaiting = await TableAsync(world, SaleViews.AwaitingHandover);
        Assert.Equal((10, 11, 0), (approved.Counts.Approved, awaiting.Counts.AwaitingHandover, awaiting.Counts.Delivered));
        Assert.Equal(10, approved.Total);
        Assert.Equal(11, awaiting.Total);
        Assert.Equal(SaleViews.Approved, (await world.Lists.TableAsync(world.Manager,
            new(1, 10, null, null, null, false), default)).Value!.View);
        Assert.All(approved.Items, item => Assert.Equal("Approved", item.Status));
        Assert.All(awaiting.Items, item => Assert.Equal("Settled", item.Status));
        var next = await world.Lists.TableAsync(world.Manager,
            new(2, 10, SaleViews.AwaitingHandover, null, null, false), default);
        Assert.Equal(holds[^1].Id, Assert.Single(next.Value!.Items).Id);
        var searched = (await world.Lists.QueueAsync(world.Agent, SaleViews.ToClaim, holds[0].Number, 1, 10, default)).Value!;
        Assert.Equal((1, 1), (searched.Total, searched.Counts.Settling));
        Assert.Equal("Settled", Assert.Single(searched.Items).Status);

        // Reuse the same purchased vehicles to prove both queues beyond two pages without another fixture baseline.
        foreach (var held in holds)
        {
            var sale = (await world.Reads.GetAsync(world.Agent, held.Id, default)).Value!;
            if (sale.Sale.Status == SalesOrderStatus.Approved)
            {
                await SettleAsync(world, held);
            }

        }

        var settledIds = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = (await world.Lists.TableAsync(world.Manager,
                new(page, 10, SaleViews.AwaitingHandover, null, null, false), default)).Value!;
            Assert.Equal(21, result.Total);
            settledIds.AddRange(result.Items.Select(item => item.Id));
        }

        var settlementOrder = holds.Where((_, index) => index % 2 == 0)
            .Concat(holds.Where((_, index) => index % 2 != 0)).Select(item => item.Id);
        Assert.Equal(settlementOrder, settledIds);
        foreach (var held in holds)
        {
            await DeliverAsync(world, held);
        }

        await VerifyPagesAsync(world, SaleViews.Completed, [.. holds.Select(item => item.Id).Reverse()]);
        var completed = (await world.Lists.QueueAsync(world.Agent, SaleViews.Completed, null, 1, 10, default)).Value!;
        Assert.Equal((0, 21), (completed.Counts.Settling, completed.Counts.Completed));
        var delivered = await TableAsync(world, SaleViews.Delivered);
        Assert.Equal((0, 0, 21), (delivered.Counts.Approved, delivered.Counts.AwaitingHandover, delivered.Counts.Delivered));
        Assert.Equal(SaleViews.Delivered, (await world.Lists.TableAsync(world.Manager,
            new(1, 10, null, null, null, false), default)).Value!.View);
        var filtered = await world.Lists.TableAsync(world.Manager,
            new(1, 10, SaleViews.Delivered, null, "balance", true) { Reference = holds[0].Number }, default);
        Assert.Empty(filtered.Value!.Items); // Submitted sales use VPO as their displayed reference.
        filtered = await world.Lists.TableAsync(world.Manager,
            new(1, 10, SaleViews.Delivered, holds[0].Number, "balance", true), default);
        Assert.Equal(holds[0].Id, Assert.Single(filtered.Value!.Items).Id);
        Assert.Equal(delivered.Counts, filtered.Value.Counts);
        var noTimer = await world.Lists.TableAsync(world.Manager,
            new(1, 10, SaleViews.Delivered, null, null, false) { MinimumHoursLeft = 0m }, default);
        Assert.Equal(0, noTimer.Value!.Total);

        // Equal discounts exercise the unique identity tie-breaker through real SQL paging.
        var tied = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await world.Lists.TableAsync(world.Manager,
                new(page, 10, SaleViews.Delivered, null, "discount", false), default);
            tied.AddRange(result.Value!.Items.Select(item => item.Id));
        }

        await using var database = NewContext();
        var heldIds = holds.Select(item => item.Id).ToArray();
        var expected = await database.SalesOrders.Where(item => heldIds.Contains(item.Id))
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync();
        Assert.Equal(expected, tied);
        var last = (await world.Reads.GetAsync(world.Agent, holds[^1].Id, default)).Value!;
        Assert.Equal(new SaleActions(false, false, false, false, false, false, false), last.Actions);
        Assert.All(completed.Items, item =>
        {
            Assert.Null(item.DueAt);
            Assert.Null(item.PausedSeconds);
            Assert.Equal("Delivered", item.Outcome);
        });
        await VerifyCurrentScopeAndHttpAsync(world);
    }

    private async Task VerifyCurrentScopeAndHttpAsync(SaleWorld world)
    {
        var held = await ApprovedAsync(world);
        await SettleAsync(world, held);
        Assert.Equal(SaleViews.AwaitingHandover, (await world.Lists.TableAsync(world.Manager,
            new(1, 10, null, null, null, false), default)).Value!.View);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.AssignAsync(world.Manager, held.Id,
            new(Guid.NewGuid(), 4, world.Colleague), default)).Status);
        Assert.Empty((await world.Lists.QueueAsync(world.Agent, SaleViews.Settling, held.Number, 1, 10, default)).Value!.Items);
        Assert.Equal(held.Id, Assert.Single((await world.Lists.QueueAsync(world.Colleague, SaleViews.Settling,
            held.Number, 1, 10, default)).Value!.Items).Id);
        Assert.Empty((await world.Lists.QueueAsync(world.OtherBranchAgent, SaleViews.Settling,
            held.Number, 1, 10, default)).Value!.Items);
        Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.QueueAsync(held.CustomerId,
            SaleViews.Completed, null, 1, 10, default)).Status);

        await using var database = NewContext();
        var otherBranch = await database.Users.Where(item => item.Id == world.OtherBranchAgent)
            .Select(item => item.BranchId).SingleAsync();
        var otherManager = await ReservationTestData.ProvisionAsync(world.Services, otherBranch, AccountRoles.BranchManager);
        var generalManager = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.GeneralManager);
        var query = new SaleTableQuery(1, 10, SaleViews.AwaitingHandover, held.Number, null, false);
        Assert.Empty((await world.Lists.TableAsync(otherManager, query, default)).Value!.Items);
        Assert.Equal(held.Id, Assert.Single((await world.Lists.TableAsync(generalManager, query, default)).Value!.Items).Id);
        await database.Users.Where(item => item.Id == world.Colleague)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, otherBranch));
        try
        {
            var moved = (await world.Lists.QueueAsync(world.Colleague, SaleViews.Settling,
                held.Number, 1, 10, default)).Value!;
            Assert.Equal((0, 0), (moved.Total, moved.Counts.Settling));
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Colleague)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.BranchId, held.BranchId));
        }

        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff/sales/queue?view=Completed")).StatusCode);
        await LoginAsync(client, world.Colleague);
        var response = await client.GetAsync($"/api/staff/sales/queue?view=Settling&search={held.Number}&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(held.Id, Assert.Single((await response.Content.ReadFromJsonAsync<SaleQueuePage>())!.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/sales/queue?view=Approved")).StatusCode);
        await LoginAsync(client, world.Manager);
        response = await client.GetAsync($"/api/staff/sales?view=AwaitingHandover&search={held.Number}&sort=balance&direction=desc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(held.Id, Assert.Single((await response.Content.ReadFromJsonAsync<SaleTablePage>())!.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/sales?view=Completed")).StatusCode);

        await DeliverAsync(world, held, world.Colleague);
        await LoginAsync(client, world.Colleague);
        response = await client.GetAsync($"/api/staff/sales/queue?view=Completed&search={held.Number}");
        Assert.Equal(held.Id, Assert.Single((await response.Content.ReadFromJsonAsync<SaleQueuePage>())!.Items).Id);
        await LoginAsync(client, world.Manager);
        var delivered = await client.GetFromJsonAsync<SaleTablePage>($"/api/staff/sales?view=Delivered&search={held.Number}");
        Assert.Equal(held.Id, Assert.Single(delivered!.Items).Id);
        await database.Users.Where(item => item.Id == world.Colleague)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.QueueAsync(world.Colleague,
                SaleViews.Completed, null, 1, 10, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == world.Colleague)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }

        await database.Branches.Where(item => item.Id == held.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.QueueAsync(world.Colleague,
                SaleViews.Completed, null, 1, 10, default)).Status);
            Assert.Equal(SaleResultStatus.Forbidden, (await world.Lists.TableAsync(world.Manager,
                query with { View = SaleViews.Delivered }, default)).Status);
            Assert.Equal(held.Id, Assert.Single((await world.Lists.TableAsync(generalManager,
                query with { View = SaleViews.Delivered }, default)).Value!.Items).Id);
        }
        finally
        {
            await database.Branches.Where(item => item.Id == held.BranchId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }
    }

    private static async Task VerifyPagesAsync(SaleWorld world, string view, Guid[] expected)
    {
        var ids = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = (await world.Lists.QueueAsync(world.Agent, view, null, page, 10, default)).Value!;
            Assert.Equal(21, result.Total);
            Assert.Equal(Math.Clamp(expected.Length - (page - 1) * 10, 0, 10), result.Items.Count);
            ids.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(expected, ids);
    }

    private static async Task<Reservation> ApprovedAsync(SaleWorld world)
    {
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 2), default)).Status);
        return held;
    }

    private async Task SettleAsync(SaleWorld world, Reservation held)
    {
        await using var database = NewContext();
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == held.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == held.VehicleId);
        var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress,
            SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43879",
            DateOnly.FromDateTime(DateTime.UtcNow), null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), sale.Version, payment, review), default)).Status);
    }

    private static async Task DeliverAsync(SaleWorld world, Reservation held, Guid? agent = null)
    {
        var currentAgent = agent ?? world.Agent;
        var sale = (await world.Reads.GetAsync(currentAgent, held.Id, default)).Value!;
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(currentAgent, held.Id, new(Guid.NewGuid(), sale.Sale.Version,
                new(sale.Context.MileageKilometres, 2, true, true, true)), default)).Status);
    }

    private static async Task<SaleTablePage> TableAsync(SaleWorld world, string view) =>
        (await world.Lists.TableAsync(world.Manager, new(1, 10, view, null, null, false), default)).Value!;

    private async Task LoginAsync(HttpClient client, Guid actorId)
    {
        await using var database = NewContext();
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
