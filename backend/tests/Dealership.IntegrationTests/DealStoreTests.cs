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
        var email = (await database.Users.AsNoTracking().SingleAsync(item => item.Id == customer)).Email!;
        var vehicleId = listing.Publication.VehicleId;
        var deals = services.GetRequiredService<DealService>();
        var walkIn = new DealCreateRequest(Guid.NewGuid(), vehicleId, null, $"  {email}  ");

        foreach (var actor in new[] { manager, technician, generalManager, customer, Guid.NewGuid() })
        {
            Assert.Equal(DealStatus.Forbidden, (await deals.CreateAsync(actor, walkIn, default)).Status);
        }

        Assert.Equal(DealStatus.Invalid, (await deals.CreateAsync(agent, walkIn with { CustomerEmail = null }, default)).Status);
        Assert.Equal(DealStatus.Invalid,
            (await deals.CreateAsync(agent, walkIn with { ReservationId = Guid.NewGuid() }, default)).Status);
        Assert.Equal(DealStatus.NotFound,
            (await deals.CreateAsync(agent, walkIn with { CustomerEmail = "nobody@example.invalid" }, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.CreateAsync(agent, walkIn with { VehicleId = Guid.NewGuid() }, default)).Status);

        var created = await deals.CreateAsync(agent, walkIn, default);
        Assert.Equal(DealStatus.Succeeded, created.Status);
        var deal = created.Deal!;
        Assert.Equal(("Draft", customer, agent, 1L, null), (deal.Status, deal.CustomerId, deal.SalesAgentId, deal.Version,
            deal.OrderNumber));
        Assert.Equal(new[] { "Created" }, deal.Events.Select(item => item.Kind));
        Assert.Equal(deal.Id, (await deals.CreateAsync(agent, walkIn, default)).Deal!.Id);
        Assert.Equal(DealStatus.Conflict, (await deals.CreateAsync(otherAgent, walkIn, default)).Status);
        Assert.Equal(DealStatus.Conflict,
            (await deals.CreateAsync(agent, walkIn with { Id = Guid.NewGuid() }, default)).Status);

        var revise = new DealReviseRequest(1, null, 300m, null, 0m);
        Assert.Equal(DealStatus.NotFound, (await deals.ReviseAsync(otherAgent, deal.Id, revise, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.ReviseAsync(manager, deal.Id, revise, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { NegotiatedDiscount = -1m }, default)).Status);
        Assert.Equal(DealStatus.Invalid, (await deals.ReviseAsync(agent, deal.Id, revise with { NegotiatedDiscount = 999999999m }, default)).Status);
        var revised = await deals.ReviseAsync(agent, deal.Id, revise, default);
        Assert.Equal((DealStatus.Succeeded, 2L, 300m), (revised.Status, revised.Deal!.Version, revised.Deal.NegotiatedDiscount));
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

        // Cancelling frees the vehicle for a new deal, which receives the next number once submitted.
        var next = await deals.CreateAsync(otherAgent, new(Guid.NewGuid(), vehicleId, null, email), default);
        Assert.Equal(DealStatus.Succeeded, next.Status);
        var nextSubmitted = await deals.TransitionAsync(otherAgent, next.Deal!.Id, DealAction.Submit, new(1, null), default);
        Assert.Equal($"VPO-{year}-000002", nextSubmitted.Deal!.OrderNumber);

        Assert.Equal(1, (await deals.ListAsync(agent, 1, 25, null, null, default)).Page!.Total);
        Assert.Equal(1, (await deals.ListAsync(otherAgent, 1, 25, null, null, default)).Page!.Total);
        Assert.Equal(2, (await deals.ListAsync(manager, 1, 25, null, null, default)).Page!.Total);
        Assert.Equal(1, (await deals.ListAsync(generalManager, 1, 25, "Cancelled", null, default)).Page!.Total);
        Assert.Equal(1, (await deals.ListAsync(generalManager, 1, 25, null, "000002", default)).Page!.Total);
        Assert.Equal(DealStatus.Invalid, (await deals.ListAsync(manager, 1, 25, "Bogus", null, default)).Status);
        Assert.Equal(DealStatus.NotFound, (await deals.GetAsync(agent, next.Deal.Id, default)).Status);
        Assert.Equal(DealStatus.Forbidden, (await deals.GetAsync(technician, deal.Id, default)).Status);

        await RejectedAsync("UPDATE \"SalesOrderEvents\" SET \"Reason\" = 'rewritten'", PostgresErrorCodes.RestrictViolation);
        await RejectedAsync("DELETE FROM \"SalesOrderEvents\"", PostgresErrorCodes.InsufficientPrivilege);
        await RejectedAsync("UPDATE \"SalesOrders\" SET \"BalancePayable\" = 1", PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"SalesOrders\" SET \"Status\" = 'Draft', \"DecidedById\" = NULL, \"DiscountLimitAtSubmit\" = NULL WHERE \"Status\" = 'Cancelled'",
            PostgresErrorCodes.UniqueViolation);
    }

    private static async Task<Reservation> HoldAsync(CheckInTestData data, ListingPublication publication)
    {
        using var scope = data.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(data.OwnerId,
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

