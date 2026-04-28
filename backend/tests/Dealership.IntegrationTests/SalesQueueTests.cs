using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class SalesQueueTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AgentsSeeTheirOwnQueueTabsWithCountsAndManagersSeeOpenAndHandledHolds()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var colleague = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var first = await CreateListingAsync(data, services, manager, technician, 0);
        var second = await CreateListingAsync(data, services, manager, technician, 1);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var firstHold = await HoldAsync(data, await RegisterCustomerAsync(services, database), first.Publication);
        var secondHold = await HoldAsync(data, await RegisterCustomerAsync(services, database), second.Publication);
        var deals = services.GetRequiredService<DealService>();

        var open = (await deals.QueueAsync(agent, null, 1, 25, default)).Queue!;
        Assert.Equal(new SalesQueueCounts(2, 0, 0), open.Counts);
        // Open holds list the soonest expiry first, with the running deadline and no frozen time.
        Assert.Equal(new[] { firstHold.Id, secondHold.Id }, open.Items.Select(item => item.ReservationId));
        Assert.All(open.Items, item =>
        {
            Assert.NotNull(item.ExpiresAtUtc);
            Assert.Null(item.FrozenRemainingSeconds);
            Assert.Null(item.DealId);
        });
        Assert.Equal("Hafiz Rahman", open.Items[0].Customer);
        var later = (await deals.QueueAsync(agent, "ToClaim", 2, 10, default)).Queue!;
        Assert.Empty(later.Items);
        Assert.Equal((2, 2, 10), (later.Total, later.Page, later.PageSize));

        var claimed = await deals.ClaimAsync(agent, firstHold.Id, new(Guid.NewGuid()), default);
        Assert.Equal(DealStatus.Succeeded, claimed.Status);
        var handling = (await deals.QueueAsync(agent, "Handling", 1, 25, default)).Queue!;
        Assert.Equal(new SalesQueueCounts(1, 1, 0), handling.Counts);
        var working = Assert.Single(handling.Items);
        Assert.Equal((firstHold.Id, claimed.Deal!.Id, "Draft", "Add terms and submit"),
            (working.ReservationId, working.DealId, working.DealStatus, working.NextStep));
        Assert.Null(working.ExpiresAtUtc);
        Assert.True(working.FrozenRemainingSeconds is > 0 and <= 48 * 3600);
        Assert.NotNull(working.ClaimedAtUtc);
        Assert.Equal(new SalesQueueCounts(1, 0, 0), (await deals.QueueAsync(colleague, "Handling", 1, 25, default)).Queue!.Counts);

        // Submitting a discount above the limit moves the deal to Waiting, out of Handling.
        var revised = await deals.ReviseAsync(agent, claimed.Deal.Id, new(claimed.Deal.Version, null, 800m, null, 0m), default);
        var submitted = await deals.TransitionAsync(agent, claimed.Deal.Id, DealAction.Submit, new(revised.Deal!.Version, null), default);
        Assert.Equal("PendingManagerApproval", submitted.Deal!.Status);
        var waiting = (await deals.QueueAsync(agent, "Waiting", 1, 25, default)).Queue!;
        Assert.Equal(new SalesQueueCounts(1, 0, 1), waiting.Counts);
        Assert.Equal("Awaiting manager approval", Assert.Single(waiting.Items).NextStep);
        Assert.Empty((await deals.QueueAsync(agent, "Handling", 1, 25, default)).Queue!.Items);

        Assert.Equal(DealStatus.Forbidden, (await deals.QueueAsync(manager, null, 1, 25, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.QueueAsync(technician, null, 1, 25, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.QueueAsync(agent, "Everything", 1, 25, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.QueueAsync(agent, null, 1, 7, default)).Status);

        // Managers read the same holds as Open and Being handled, with who is handling each.
        var reads = services.GetRequiredService<IReservationReadStore>();
        var all = (await reads.StaffAsync(manager, new(IsActive: true), default)).History!;
        Assert.Equal((2, 0, 1, 1), (all.Counts!.Active, all.Counts.History, all.Counts.Open, all.Counts.Handled));
        var handled = (await reads.StaffAsync(manager, new(IsActive: true, Claimed: true), default)).History!;
        var handledItem = Assert.Single(handled.Items);
        Assert.Equal(firstHold.Id, handledItem.Receipt.Id);
        Assert.Equal($"staff.{agent:N}@example.invalid", handledItem.Claim!.Agent);
        Assert.True(handledItem.Claim.FrozenRemainingSeconds > 0);
        var unclaimed = (await reads.StaffAsync(manager, new(IsActive: true, Claimed: false), default)).History!;
        var unclaimedItem = Assert.Single(unclaimed.Items);
        Assert.Equal(secondHold.Id, unclaimedItem.Receipt.Id);
        Assert.Null(unclaimedItem.Claim);
        // The counts describe the whole scope, whichever tab is read.
        Assert.Equal((2, 1, 1), (handled.Counts!.Active, handled.Counts.Open, handled.Counts.Handled));
    }

    private static async Task<Reservation> HoldAsync(CheckInTestData data, Guid customerId, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(customerId,
            publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
