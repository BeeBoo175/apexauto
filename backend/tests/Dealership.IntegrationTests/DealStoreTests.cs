using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class DealStoreTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task LifecycleEnforcesScopeVersionsNumbersApprovalAndImmutableHistory()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var otherAgent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var generalManager = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        database.PurchaseSettings.Add(new(500m, 0.08m, manager, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var customer = await RegisterCustomerAsync(services, database);
        var vehicleId = listing.Publication.VehicleId;
        var deals = services.GetRequiredService<DealService>();
        var held = await HoldAsync(data, customer, listing.Publication);
        var claim = new DealClaimRequest(Guid.NewGuid());

        foreach (var actor in new[] { manager, technician, generalManager, customer, Guid.NewGuid() })
        {
            Assert.Equal(DealStatus.Forbidden, (await deals.ClaimAsync(actor, held.Id, claim, default)).Status);
        }

        Assert.Equal(DealStatus.Invalid, (await deals.ClaimAsync(agent, Guid.Empty, claim, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.ClaimAsync(agent, Guid.NewGuid(), claim, default)).Status);

        var created = await deals.ClaimAsync(agent, held.Id, claim, default);
        Assert.Equal(DealStatus.Succeeded, created.Status);
        var deal = created.Deal!;
        Assert.Equal(("Draft", customer, agent, 1L, null), (deal.Status, deal.CustomerId, deal.SalesAgentId, deal.Version,
            deal.OrderNumber));
        Assert.Equal(new[] { "Created" }, deal.Events.Select(item => item.Kind));
        Assert.Equal(deal.Id, (await deals.ClaimAsync(agent, held.Id, claim, default)).Deal!.Id);
        Assert.Equal(DealStatus.Conflict, (await deals.ClaimAsync(otherAgent, held.Id, claim, default)).Status);
        Assert.Equal(DealStatus.Conflict,
            (await deals.ClaimAsync(agent, held.Id, new(Guid.NewGuid()), default)).Status);

        var revise = new DealReviseRequest(1, null, 300m, null, 0m);
        Assert.Equal(DealStatus.NotFound, (await deals.ReviseAsync(otherAgent, deal.Id, revise, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.ReviseAsync(manager, deal.Id, revise, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { NegotiatedDiscount = -1m }, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { NegotiatedDiscount = 999999999m }, default)).Status);
        var revised = await deals.ReviseAsync(agent, deal.Id, revise, default);
        Assert.Equal((DealStatus.Succeeded, 2L, 300m), (revised.Status, revised.Deal!.Version, revised.Deal.NegotiatedDiscount));
        var recorded = (await deals.DealHistoryAsync(manager, deal.Id, 1, 25, default)).History!;
        // The deal's receipts use the application clock and the hold's the database clock, so order is not asserted here.
        Assert.Equal(new[] { "Claimed", "Revised" }, recorded.Items.Select(item => item.Kind).Order());
        var revision = recorded.Items.Single(item => item.Kind == "Revised");
        Assert.Equal(("Deal", $"staff.{agent:N}@example.invalid"), (revision.Source, revision.Actor));
        Assert.Equal(new[] { new DealChange(DealChange.NegotiatedDiscount, "0.00", "300.00") }, revision.Changes);
        Assert.Empty(recorded.Items.Single(item => item.Kind == "Claimed").Changes);
        Assert.Equal(DealStatus.NotFound, (await deals.DealHistoryAsync(otherAgent, deal.Id, 1, 25, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.DealHistoryAsync(manager, deal.Id, 1, 7, default)).Status);
        var stale = await deals.ReviseAsync(agent, deal.Id, revise, default);
        Assert.Equal((DealStatus.Conflict, 2L), (stale.Status, stale.Deal!.Version));

        Assert.Equal(DealStatus.Forbidden, (await deals.TransitionAsync(manager, deal.Id, DealAction.Submit, new(2, null), default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.TransitionAsync(agent, deal.Id, DealAction.Approve, new(2, null), default)).Status);
        var submitted = await deals.TransitionAsync(agent, deal.Id, DealAction.Submit, new(2, null), default);
        var year = DateTimeOffset.UtcNow.Year;
        Assert.Equal(("Approved", $"VPO-{year}-000001", 3L), (submitted.Deal!.Status, submitted.Deal.OrderNumber, submitted.Deal.Version));
        Assert.Equal(DealStatus.Conflict, (await deals.TransitionAsync(agent, deal.Id, DealAction.Submit, new(3, null), default)).Status);

        var raised = await deals.ReviseAsync(agent, deal.Id, revise with { ExpectedVersion = 3, NegotiatedDiscount = 800m }, default);
        Assert.Equal(("Draft", 4L), (raised.Deal!.Status, raised.Deal.Version));
        var pending = await deals.TransitionAsync(agent, deal.Id, DealAction.Submit, new(4, null), default);
        Assert.Equal(("PendingManagerApproval", $"VPO-{year}-000001", 500m), (pending.Deal!.Status, pending.Deal.OrderNumber,
            pending.Deal.DiscountLimitAtSubmit));
        Assert.Equal(DealStatus.Forbidden, (await deals.TransitionAsync(agent, deal.Id, DealAction.Approve, new(5, null), default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.TransitionAsync(otherAgent, deal.Id, DealAction.Approve, new(5, null), default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.TransitionAsync(manager, deal.Id, DealAction.Reject, new(5, " "), default)).Status);
        var rejected = await deals.TransitionAsync(manager, deal.Id, DealAction.Reject, new(5, "Margin too thin"), default);
        Assert.Equal(("Draft", 6L, null), (rejected.Deal!.Status, rejected.Deal.Version, rejected.Deal.DecidedById));
        Assert.Equal("Margin too thin", rejected.Deal.Events[^1].Reason);

        Assert.Equal("PendingManagerApproval", (await deals.TransitionAsync(agent, deal.Id, DealAction.Submit, new(6, null), default)).Deal!.Status);
        var approved = await deals.TransitionAsync(generalManager, deal.Id, DealAction.Approve, new(7, null), default);
        Assert.Equal(("Approved", generalManager), (approved.Deal!.Status, approved.Deal.DecidedById));
        Assert.Equal(new[] { "Created", "Revised", "Submitted", "Revised", "Submitted", "Rejected", "Submitted", "Approved" },
            approved.Deal.Events.Select(item => item.Kind));

        Assert.Equal(DealStatus.Invalid, (await deals.TransitionAsync(agent, deal.Id, DealAction.Cancel, new(8, null), default)).Status);
        var cancelled = await deals.TransitionAsync(agent, deal.Id, DealAction.Cancel, new(8, "Customer withdrew"), default);
        Assert.Equal("Cancelled", cancelled.Deal!.Status);
        Assert.Equal(DealStatus.Conflict, (await deals.TransitionAsync(agent, deal.Id, DealAction.Cancel, new(9, "Again"), default)).Status);

        // Cancelling refunds the hold and frees the vehicle; a new hold starts a new deal with the next number.
        await using (var refunds = NewContext())
        {
            var release = await refunds.ReservationReleases.AsNoTracking().SingleAsync(item => item.ReservationId == held.Id);
            Assert.Equal((ReservationReleaseKind.DealCancelled, agent, "Customer withdrew", 500m),
                (release.Kind, release.ActorId, release.Reason, release.Refund));
        }

        var heldAgain = await HoldAsync(data, customer, listing.Publication);
        var next = await deals.ClaimAsync(otherAgent, heldAgain.Id, new(Guid.NewGuid()), default);
        Assert.Equal(DealStatus.Succeeded, next.Status);
        var nextSubmitted = await deals.TransitionAsync(otherAgent, next.Deal!.Id, DealAction.Submit, new(1, null), default);
        Assert.Equal($"VPO-{year}-000002", nextSubmitted.Deal!.OrderNumber);

        Assert.Equal(1, (await ListAsync(deals, agent)).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, otherAgent)).Page!.Total);
        var managerList = (await ListAsync(deals, manager)).Page!;
        Assert.Equal((2, 1, 0, 1), (managerList.Total, managerList.Counts.Active, managerList.Counts.NeedsApproval,
            managerList.Counts.History));
        Assert.Equal(1, (await ListAsync(deals, generalManager, status: "Cancelled")).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, generalManager, number: "000002")).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, view: "Active")).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, view: "History")).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, view: "NeedsApproval")).Page!.Total);
        // The test clock can repeat an instant, so rows are found by identity rather than by position.
        var newest = managerList.Items.Single(item => item.Id == next.Deal!.Id);
        Assert.Equal((next.Deal!.Id, "Approved", "Approved", "Hafiz Rahman"), (newest.Id, newest.Status,
            newest.NextStep, newest.Customer));
        Assert.False(string.IsNullOrWhiteSpace(newest.Registration) || string.IsNullOrWhiteSpace(newest.Agent));
        Assert.Equal("Cancelled", managerList.Items.Single(item => item.Id == deal.Id).NextStep);
        Assert.Equal(new[] { $"VPO-{year}-000001", $"VPO-{year}-000002" },
            (await ListAsync(deals, manager, sort: "number", direction: "asc")).Page!.Items.Select(item => item.OrderNumber));
        Assert.Equal(new[] { $"VPO-{year}-000002", $"VPO-{year}-000001" },
            (await ListAsync(deals, manager, sort: "number", direction: "desc")).Page!.Items.Select(item => item.OrderNumber));
        Assert.Equal(0, (await ListAsync(deals, manager, vehicle: "zzzz")).Page!.Total);
        Assert.Equal(2, (await ListAsync(deals, manager, customer: "HAFIZ")).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, customer: "%")).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, search: "000002")).Page!.Total);
        Assert.Equal(2, (await ListAsync(deals, manager, search: newest.Registration[1..^1])).Page!.Total);
        Assert.Equal(2, (await ListAsync(deals, manager, search: " HAFIZ ")).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, search: "hafiz", view: "History")).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, search: "%")).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, search: "zzzz")).Page!.Total);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, search: new string('x', 161))).Status);
        DealColumnFilters Columns(string? agent = null, string? branch = null, string? nextStep = null,
            decimal? minBalance = null, decimal? maxBalance = null, decimal? minDiscount = null,
            decimal? maxDiscount = null, string? updatedFrom = null, string? updatedTo = null,
            string? registration = null) =>
            new(agent, branch, nextStep, minBalance, maxBalance, minDiscount, maxDiscount, null, null, updatedFrom,
                updatedTo, registration);
        Assert.Equal(2, (await ListAsync(deals, manager,
            columns: Columns(registration: newest.Registration[1..^1]))).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, columns: Columns(registration: "zzzz"))).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, columns: Columns(agent: $"staff.{agent:N}"))).Page!.Total);
        Assert.Equal(2, (await ListAsync(deals, manager, columns: Columns(branch: newest.Branch[..3]))).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, columns: Columns(branch: "zzzz"))).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, columns: Columns(nextStep: "Approved"))).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, columns: Columns(nextStep: "Cancelled"))).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, columns: Columns(nextStep: "Edit and resubmit"))).Page!.Total);
        Assert.Contains(newest.Id, (await ListAsync(deals, manager,
            columns: Columns(minBalance: newest.BalancePayable, maxBalance: newest.BalancePayable))).Page!.Items.Select(item => item.Id));
        Assert.Equal(1, (await ListAsync(deals, manager, columns: Columns(minDiscount: 700m))).Page!.Total);
        Assert.Equal(1, (await ListAsync(deals, manager, columns: Columns(maxDiscount: 100m))).Page!.Total);
        Assert.Equal(0, (await ListAsync(deals, manager, columns: Columns(updatedFrom: "2000-01-01", updatedTo: "2000-01-02"))).Page!.Total);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, columns: Columns(nextStep: "Bogus"))).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, columns: Columns(minBalance: 10m, maxBalance: 5m))).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, columns: Columns(updatedFrom: "2026-01-01"))).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, status: "Bogus")).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, view: "Everything")).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, sort: "bogus")).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, direction: "desc")).Status);
        Assert.Equal(DealStatus.Invalid, (await ListAsync(deals, manager, size: 7)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.OptionsAsync(technician, deal.Id, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.OptionsAsync(agent, next.Deal.Id, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.GetAsync(agent, next.Deal.Id, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.GetAsync(technician, deal.Id, default)).Status);

        await RejectedAsync("UPDATE \"SalesOrderEvents\" SET \"Reason\" = 'rewritten'", PostgresErrorCodes.RestrictViolation);
        await RejectedAsync("DELETE FROM \"SalesOrderEvents\"", PostgresErrorCodes.InsufficientPrivilege);
        await RejectedAsync("UPDATE \"SalesOrders\" SET \"BalancePayable\" = 1", PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"SalesOrders\" SET \"Status\" = 'Draft', \"DecidedById\" = NULL, \"DiscountLimitAtSubmit\" = NULL WHERE \"Status\" = 'Cancelled'",
            PostgresErrorCodes.UniqueViolation);
    }

    private static Task<DealResult> ListAsync(DealService deals, Guid actor, string? view = null, string? status = null,
        string? sort = null, string? direction = null, string? number = null, string? vehicle = null,
        string? customer = null, int page = 1, int size = 25, string? search = null, DealColumnFilters? columns = null) =>
        deals.ListAsync(actor, page, size, view, status, sort, direction, number, vehicle, customer, search,
            columns ?? new DealColumnFilters(null, null, null, null, null, null, null, null, null, null, null), default);

    private static async Task<Reservation> HoldAsync(CheckInTestData data, Guid customerId, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(customerId,
            publication.VehicleId, new(Guid.NewGuid(), publication.Id, null), CancellationToken.None);
        Assert.Equal(ReservationStatus.Succeeded, result.Status);
        return result.Reservation!;
    }

    private async Task RejectedAsync(string sql, string state)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}

