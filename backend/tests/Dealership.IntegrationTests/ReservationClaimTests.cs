using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class ReservationClaimTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClaimingFreezesTheHoldAndManagersMoveOrReleaseItWithRetainedHistory()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var colleague = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var generalManager = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var customer = await RegisterCustomerAsync(services, database);
        var held = await HoldAsync(data, customer, listing.Publication);
        var deals = services.GetRequiredService<DealService>();

        // Only the sales agent claims for themselves; managers assign; nobody outside the hold's branch acts.
        Assert.Equal(DealStatus.Forbidden, (await deals.ClaimAsync(manager, held.Id, new(Guid.NewGuid()), default)).Status);
        Assert.Equal(DealStatus.Forbidden,
            (await deals.AssignAsync(agent, held.Id, new(Guid.NewGuid(), colleague), default)).Status);
        Assert.Equal(DealStatus.Forbidden,
            (await deals.AssignAsync(technician, held.Id, new(Guid.NewGuid(), agent), default)).Status);
        var otherBranch = await AnotherBranchAsync(database);
        var otherBranchAgent = await ProvisionAsync(services, otherBranch, AccountRoles.SalesAgent);
        var otherBranchManager = await ProvisionAsync(services, otherBranch, AccountRoles.BranchManager);

        // Managers choose the assignee from the hold's own branch; agents and other branches cannot list them.
        foreach (var lister in new[] { manager, generalManager })
        {
            var listed = await deals.AgentsAsync(lister, held.Id, default);
            Assert.Equal(DealStatus.Succeeded, listed.Status);
            var listedIds = listed.Agents!.Select(item => item.Id).ToList();
            Assert.Contains(agent, listedIds);
            Assert.Contains(colleague, listedIds);
            Assert.DoesNotContain(otherBranchAgent, listedIds);
            Assert.DoesNotContain(technician, listedIds);
            Assert.DoesNotContain(manager, listedIds);
            Assert.Equal(listed.Agents!.Select(item => item.Email).Order(StringComparer.Ordinal),
                listed.Agents!.Select(item => item.Email));
        }

        Assert.Equal(DealStatus.Forbidden, (await deals.AgentsAsync(agent, held.Id, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.AgentsAsync(technician, held.Id, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.AgentsAsync(otherBranchManager, held.Id, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.AgentsAsync(manager, Guid.NewGuid(), default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.AgentsAsync(manager, Guid.Empty, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.ClaimAsync(otherBranchAgent, held.Id, new(Guid.NewGuid()), default)).Status);
        Assert.Equal(DealStatus.Invalid,
            (await deals.AssignAsync(manager, held.Id, new(Guid.NewGuid(), otherBranchAgent), default)).Status);

        var claim = new DealClaimRequest(Guid.NewGuid());
        var claimed = await deals.ClaimAsync(agent, held.Id, claim, default);
        Assert.Equal(DealStatus.Succeeded, claimed.Status);
        Assert.Equal((agent, "Draft", held.Id), (claimed.Deal!.SalesAgentId, claimed.Deal.Status, claimed.Deal.ReservationId));
        Assert.Null(claimed.Deal.Context!.HoldExpiresAtUtc);
        Assert.Equal(claimed.Deal.Id, (await deals.ClaimAsync(agent, held.Id, claim, default)).Deal!.Id);
        Assert.Equal(DealStatus.Conflict, (await deals.ClaimAsync(colleague, held.Id, new(Guid.NewGuid()), default)).Status);

        await using (var frozen = NewContext())
        {
            var hold = await frozen.Reservations.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.Equal((agent, true), (hold.ClaimedById, hold.FrozenRemaining > TimeSpan.Zero));
            Assert.Equal(hold.ExpiresAt, hold.CreatedAt.AddHours(48));
            Assert.False(hold.HasExpired(hold.ExpiresAt.AddDays(30)));
        }

        // Managers find a claimed hold by who handles it and order the handled tab by agent or claim time.
        var reads = services.GetRequiredService<IReservationReadStore>();
        var agentEmail = $"staff.{agent:N}@example.invalid";
        var handled = (await reads.StaffAsync(manager,
            new(IsActive: true, Claimed: true, Agent: agentEmail), default)).History!;
        Assert.Equal(held.Id, Assert.Single(handled.Items).Receipt.Id);
        Assert.Equal(agentEmail, handled.Items[0].Claim!.Agent);
        Assert.True(handled.Counts!.Handled >= 1);
        Assert.Empty((await reads.StaffAsync(manager,
            new(IsActive: true, Claimed: true, Agent: $"staff.{colleague:N}"), default)).History!.Items);
        Assert.Contains(held.Id, (await reads.StaffAsync(manager,
            new(IsActive: true, Search: agentEmail), default)).History!.Items.Select(item => item.Receipt.Id));
        foreach (var claimedTab in new[] { true, false })
        {
            // Without a chosen sort the open and handled tabs keep their queue order.
            var queue = await reads.StaffAsync(manager, new(IsActive: true, Claimed: claimedTab), default);
            Assert.Equal(ReservationStatus.Succeeded, queue.Status);
            Assert.Equal(claimedTab, queue.History!.Items.Any(item => item.Receipt.Id == held.Id));
        }

        foreach (var sort in new[] { "agent", "claimed" })
        {
            var ordered = await reads.StaffAsync(manager,
                new(IsActive: true, Claimed: true, Sort: sort, Direction: "desc"), default);
            Assert.Equal(ReservationStatus.Succeeded, ordered.Status);
            Assert.Contains(held.Id, ordered.History!.Items.Select(item => item.Receipt.Id));
        }

        // A claimed hold has a deal in progress, so it cannot be cancelled as a bare hold.
        var cancellations = services.GetRequiredService<ReservationCancellationService>();
        Assert.Equal(ReservationStatus.Conflict,
            (await cancellations.CancelAsync(manager, held.Id, new(Guid.NewGuid(), "Customer asked"), default)).Status);

        // Managers move the claim; an agent cannot release someone else's, and a repeat changes nothing.
        var move = new DealAssignRequest(Guid.NewGuid(), colleague);
        var moved = await deals.AssignAsync(manager, held.Id, move, default);
        Assert.Equal((DealStatus.Succeeded, colleague, 2L), (moved.Status, moved.Deal!.SalesAgentId, moved.Deal.Version));
        Assert.Equal(new[] { "Created", "Reassigned" }, moved.Deal.Events.Select(item => item.Kind));
        Assert.Equal(2L, (await deals.AssignAsync(manager, held.Id, move with { Id = Guid.NewGuid() }, default)).Deal!.Version);
        Assert.Equal(DealStatus.NotFound, (await deals.GetAsync(agent, moved.Deal.Id, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.ReleaseAsync(agent, held.Id, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.ReleaseAsync(technician, held.Id, default)).Status);

        var released = await deals.ReleaseAsync(colleague, held.Id, default);
        Assert.Equal((DealStatus.Succeeded, "Cancelled"), (released.Status, released.Deal!.Status));
        Assert.Equal("Claim released", released.Deal.Events[^1].Reason);
        Assert.Equal(DealStatus.Conflict, (await deals.ReleaseAsync(colleague, held.Id, default)).Status);

        await using (var resumed = NewContext())
        {
            var hold = await resumed.Reservations.AsNoTracking().SingleAsync(item => item.Id == held.Id);
            Assert.True(hold.IsActive);
            Assert.Null(hold.ClaimedById);
            // The hold resumes with the time it had left, so it never gets a fresh 48 hours.
            Assert.True(hold.ExpiresAt >= hold.CreatedAt.AddHours(48));
            Assert.True(hold.ExpiresAt < DateTimeOffset.UtcNow.AddHours(48));
            var events = await resumed.ReservationEvents.AsNoTracking().Where(item => item.ReservationId == held.Id)
                .OrderBy(item => item.Sequence).ToListAsync();
            Assert.Equal(new[] { ReservationEventKind.Claimed, ReservationEventKind.Reassigned, ReservationEventKind.Released },
                events.Select(item => item.Kind));
            Assert.Equal(new Guid?[] { agent, colleague, null }, events.Select(item => item.AgentId));
            Assert.Equal(new[] { 1, 2, 3 }, events.Select(item => item.Sequence));
        }

        // An open hold is assigned by a manager, who starts the deal for the chosen agent.
        var assigned = await deals.AssignAsync(generalManager, held.Id, new(Guid.NewGuid(), agent), default);
        Assert.Equal((DealStatus.Succeeded, agent), (assigned.Status, assigned.Deal!.SalesAgentId));

        // Cancelling that deal releases the hold with the full simulated refund.
        var cancelled = await deals.TransitionAsync(colleague, assigned.Deal.Id, DealAction.Cancel,
            new(assigned.Deal.Version, "Customer withdrew"), default);
        Assert.Equal(DealStatus.NotFound, cancelled.Status);
        cancelled = await deals.TransitionAsync(agent, assigned.Deal.Id, DealAction.Cancel,
            new(assigned.Deal.Version, "Customer withdrew"), default);
        Assert.Equal(DealStatus.Succeeded, cancelled.Status);
        await using var after = NewContext();
        var refund = await after.ReservationReleases.AsNoTracking().SingleAsync(item => item.ReservationId == held.Id);
        Assert.Equal((ReservationReleaseKind.DealCancelled, agent, 500m), (refund.Kind, refund.ActorId, refund.Refund));
        Assert.False((await after.Reservations.AsNoTracking().SingleAsync(item => item.Id == held.Id)).IsActive);
        Assert.Equal(DealStatus.Conflict, (await deals.ClaimAsync(agent, held.Id, new(Guid.NewGuid()), default)).Status);

        // One history serves the hold and its deal: every receipt, newest first, with who acted and for whom.
        var history = (await deals.ReservationHistoryAsync(manager, held.Id, 1, 10, default)).History!;
        Assert.Equal(7, history.TotalCount);
        Assert.Equal(new[] { "Claimed", "Reassigned", "Released", "Assigned", "DealCancelled" }.Order(),
            history.Items.Where(item => item.Source != "Deal").Select(item => item.Kind).Order());
        Assert.Equal(new[] { "Cancelled", "Cancelled" }, history.Items.Where(item => item.Source == "Deal").Select(item => item.Kind));
        Assert.Equal(history.Items.OrderByDescending(item => item.RecordedAtUtc).Select(item => item.Id),
            history.Items.Select(item => item.Id));
        var reassigned = history.Items.Single(item => item.Kind == "Reassigned");
        Assert.Equal(("Hold", $"staff.{colleague:N}@example.invalid", $"staff.{agent:N}@example.invalid"),
            (reassigned.Source, reassigned.Agent, reassigned.PreviousAgent));
        Assert.Contains(history.Items, item => item.Reason == "Claim released");
        Assert.All(history.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.Empty((await deals.ReservationHistoryAsync(manager, held.Id, 2, 10, default)).History!.Items);
        Assert.Equal(history.TotalCount, (await deals.DealHistoryAsync(agent, assigned.Deal.Id, 1, 50, default)).History!.TotalCount);
        Assert.Equal(DealStatus.Succeeded, (await deals.ReservationHistoryAsync(colleague, held.Id, 1, 25, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.ReservationHistoryAsync(otherBranchAgent, held.Id, 1, 25, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.ReservationHistoryAsync(technician, held.Id, 1, 25, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReservationHistoryAsync(manager, held.Id, 0, 25, default)).Status);
    }

    private async Task<Guid> AnotherBranchAsync(DealershipDbContext database)
    {
        var other = new Dealership.Domain.Branches.Branch(Guid.NewGuid(), "ApexAuto Setia Alam",
            "5 Jalan Setia Prima, Setia Alam", "Shah Alam", "Selangor", "0333441122", DateTimeOffset.UtcNow);
        database.Branches.Add(other);
        await database.SaveChangesAsync();
        return other.Id;
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
