using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Performance;
using Dealership.Application.Sales;
using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class PerformanceReadsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task DeliveredSaleMovesTheHeadlineAndPipelineOfItsBranchManager()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var reads = world.Services.GetRequiredService<IPerformanceReads>();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DealershipLocale.Malaysia).DateTime);
        var query = PerformanceQuery.Parse(today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null)!;
        var before = (await reads.HeadlineAsync(world.Manager, query, default)).Value!;
        var pipelineBefore = (await reads.PipelineAsync(world.Manager, query, default)).Value!;

        var flowBefore = (await reads.StockFlowAsync(world.Manager, query, default)).Value!;
        var stagesBefore = (await reads.StageWaitingAsync(world.Manager, query, default)).Value!;
        var (held, request, total) = await SettledAsync(world);
        var workload = (await reads.WorkloadAsync(world.Manager, query, default)).Value!;
        var agent = workload.People.Single(person => person.Id == world.Agent);
        Assert.Equal(world.Manager, workload.Actor.UserId);
        Assert.Equal(AccountRoles.SalesAgent, agent.Role);
        Assert.True(agent.Under2Days >= 1);
        Assert.DoesNotContain(workload.People, person => person.Id == world.OtherBranchAgent);
        var stagesSettled = (await reads.StageWaitingAsync(world.Manager, query, default)).Value!;
        Assert.Equal(StageNow(stagesBefore, PerformanceStages.SaleInProgress) + 1,
            StageNow(stagesSettled, PerformanceStages.SaleInProgress));
        var settledGroup = (await reads.PipelineAsync(world.Manager, query, default)).Value!.Groups
            .Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        var settledBefore = pipelineBefore.Groups.Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        Assert.Equal(settledBefore.Count + 1, settledGroup.Count);
        Assert.Equal(settledBefore.AgreedTotal + total, settledGroup.AgreedTotal);

        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, held.Id, request, default)).Status);
        var after = (await reads.HeadlineAsync(world.Manager, query, default)).Value!;
        Assert.Equal(before.Current.VehiclesDelivered + 1, after.Current.VehiclesDelivered);
        Assert.Equal(before.Current.DeliveredValue + total, after.Current.DeliveredValue);
        Assert.Equal(before.Current.ListedCount + 1, after.Current.ListedCount);
        Assert.NotNull(after.Current.CheckInToListedDays);
        Assert.Equal(0, after.Previous.VehiclesDelivered);
        var settledAfter = (await reads.PipelineAsync(world.Manager, query, default)).Value!.Groups
            .Single(group => group.Group == PerformancePipelineGroups.SettledUndelivered);
        Assert.Equal(settledBefore.Count, settledAfter.Count);

        var stagesAfter = (await reads.StageWaitingAsync(world.Manager, query, default)).Value!;
        Assert.Equal(StageNow(stagesBefore, PerformanceStages.SaleInProgress),
            StageNow(stagesAfter, PerformanceStages.SaleInProgress));
        var delivery = stagesAfter.Stages.Single(stage => stage.Stage == PerformanceStages.SaleInProgress);
        Assert.Equal(stagesBefore.Stages.Single(stage => stage.Stage == PerformanceStages.SaleInProgress).CompletedCount + 1,
            delivery.CompletedCount);
        Assert.NotNull(delivery.AverageDays);
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.StageWaitingAsync(world.Agent, query, default)).Status);
        var workloadAfter = (await reads.WorkloadAsync(world.Manager, query, default)).Value!;
        Assert.Equal(agent.Total - 1, workloadAfter.People.Single(person => person.Id == world.Agent).Total);
        var roster = (await reads.RosterAsync(world.Manager, query, default)).Value!;
        Assert.All(roster.Cells, cell =>
        {
            Assert.InRange(cell.Weekday, 0, 6);
            Assert.InRange(cell.Hour, 0, 23);
        });
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.WorkloadAsync(world.Agent, query, default)).Status);
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.RosterAsync(world.Agent, query, default)).Status);
        var team = PerformanceTeamQuery.Parse(query, 1, 25)!;
        var salesTeam = (await reads.SalesTeamAsync(world.Manager, team, default)).Value!;
        var agentRow = salesTeam.Items.Single(row => row.PersonId == world.Agent);
        Assert.Equal(PerformancePersonStatuses.Active, agentRow.Status);
        Assert.True(agentRow.SalesSettled >= 1);
        Assert.True(agentRow.SalesDelivered >= 1);
        Assert.NotNull(agentRow.AssignmentToDeliveryDays);
        Assert.NotNull(agentRow.SalesHandled);
        Assert.Equal(salesTeam.Items.OrderBy(row => row.Name).ThenBy(row => row.PersonId).Select(row => row.PersonId),
            salesTeam.Items.Select(row => row.PersonId));
        Assert.DoesNotContain(salesTeam.Items, row => row.PersonId == world.OtherBranchAgent);
        var workshopTeam = (await reads.WorkshopTeamAsync(world.Manager, team, default)).Value!;
        Assert.All(workshopTeam.Items, row => Assert.Equal(AccountRoles.WorkshopTechnician, row.Role));
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.SalesTeamAsync(world.Agent, team, default)).Status);
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.WorkshopTeamAsync(world.Agent, team, default)).Status);
        var secondPage = (await reads.SalesTeamAsync(world.Manager, team with { Page = 99 }, default)).Value!;
        Assert.Empty(secondPage.Items);
        Assert.Equal(salesTeam.Total, secondPage.Total);
        var flowAfter = (await reads.StockFlowAsync(world.Manager, query, default)).Value!;
        Assert.Equal(flowBefore.Weeks.Sum(week => week.Deliveries) + 1, flowAfter.Weeks.Sum(week => week.Deliveries));
        Assert.All(flowAfter.Weeks, week => Assert.Equal(DayOfWeek.Monday, week.WeekStart.DayOfWeek));
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.StockFlowAsync(world.Agent, query, default)).Status);

        // Agents and customers may not read performance at all.
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.HeadlineAsync(world.Agent, query, default)).Status);
        Assert.Equal(PerformanceReadStatus.Forbidden, (await reads.PipelineAsync(held.CustomerId, query, default)).Status);
    }

    [Fact]
    public async Task TeamOutputExportsMatchThePageAndStayOutOfReachOfAgents()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        using var factory = fixture.CreateFactory();
        using var anonymous = factory.CreateClient();
        const string period = "from=2026-01-01&to=2026-12-31";
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/performance/team/sales/export?{period}")).StatusCode);

        using var agent = factory.CreateClient();
        await LoginAsync(agent, world.Agent);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await agent.GetAsync($"/api/performance/team/sales/export?{period}")).StatusCode);

        using var manager = factory.CreateClient();
        await LoginAsync(manager, world.Manager);
        var sales = await manager.GetAsync($"/api/performance/team/sales/export?{period}");
        Assert.Equal(HttpStatusCode.OK, sales.StatusCode);
        Assert.Equal("text/csv", sales.Content.Headers.ContentType!.MediaType);
        var text = await sales.Content.ReadAsStringAsync();
        Assert.StartsWith("\"Name\",\"Role\",\"Branch\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Value", text, StringComparison.Ordinal);
        var page = (await manager.GetFromJsonAsync<PerformanceTeamPage<PerformanceSalesRow>>(
            $"/api/performance/team/sales?{period}"))!;
        Assert.Equal(page.Total, int.Parse(sales.Headers.GetValues("X-Export-Row-Count").Single(), CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/performance/team/workshop/export?{period}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync("/api/performance/team/sales/export?from=2026-02-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync($"/api/performance/team/sales?{period}&pageSize=7")).StatusCode);
    }

    [Fact]
    public void PeriodsOutsideTheMalaysianCalendarLimitAreRejected()
    {
        Assert.Null(PerformanceQuery.Parse(null, null, null));
        Assert.Null(PerformanceTeamQuery.Parse(PerformanceQuery.Parse("2026-11-01", "2026-11-02", null), 1, 7));
        Assert.Null(PerformanceTeamQuery.Parse(PerformanceQuery.Parse("2026-11-01", "2026-11-02", null), 0, 25));
        Assert.Null(PerformanceQuery.Parse("2026-11-02", "2026-11-01", null));
        Assert.Null(PerformanceQuery.Parse("2025-01-01", "2026-01-02", null));
        Assert.Null(PerformanceQuery.Parse("2026-11-01", "2026-11-02", Guid.Empty));
        var period = PerformanceQuery.Parse("2026-11-01", "2026-11-07", null)!;
        Assert.Equal(TimeSpan.FromDays(7), period.Length);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 16, 0, 0, TimeSpan.Zero), period.From);
    }

    private async Task LoginAsync(HttpClient client, Guid actorId)
    {
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static int StageNow(PerformanceStageWaiting waiting, string stage) =>
        waiting.Stages.Single(item => item.Stage == stage).InStageNow;

    private async Task<(Dealership.Domain.Vehicles.Reservation Hold, SaleHandoverRequest Request, decimal Total)> SettledAsync(
        SaleWorld world)
    {
        var held = await world.HoldAsync();
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Handling.ClaimAsync(world.Agent, held.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Terms.SubmitAsync(world.Agent, held.Id, new(Guid.NewGuid(), 2), default)).Status);
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == held.Id);
        var profile = await database.CustomerProfiles.SingleAsync(item => item.UserId == sale.CustomerId);
        var vehicle = await database.Vehicles.SingleAsync(item => item.Id == sale.VehicleId);
        var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress, SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 43880", new DateOnly(2026, 11, 2),
            null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, held.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        var total = sale.BalancePayable + sale.DepositCredit + sale.TradeInAllowance;
        return (held, new(Guid.NewGuid(), 4, new(vehicle.MileageKilometres + 100, 2, true, true, true)), total);
    }
}
