using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Sales;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class SalesAwaitingDepositTableTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task TheManagerSalesTableListsRequestsAwaitingTheirCustomerUntilTheyAreCancelled()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await Post(scenario.Client, scenario.RequestPath, scenario.Request, scenario.Data.OwnerId)).StatusCode);
        var deposit = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        var query = SaleQueries.Table(new SaleTableInput { View = "awaitingDeposit" })!;

        // The request is no sale, so the other tabs ignore it, but it has its own tab, count and row.
        var page = (await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, query, default)).Value!;
        Assert.Equal(SaleViews.AwaitingDeposit, page.View);
        Assert.Equal(1, page.Counts.AwaitingDeposit);
        // Requests ride in the sale row shape: the booking staff member stands in as the agent and the lapse time as due.
        var row = Assert.Single(page.Items);
        Assert.Equal(deposit.Id, row.Id);
        Assert.Equal(SaleViews.AwaitingDeposit, row.Status);
        Assert.Equal(scenario.Data.OwnerId, row.CustomerId);
        Assert.Equal(deposit.VehicleId, row.VehicleId);
        Assert.Equal(deposit.BookedByStaffId, row.AgentId);
        Assert.Equal(deposit.ExpiresAt, row.DueAt);
        Assert.Equal(1, page.Total);

        // The table's own column filters apply to requests like they do to sales.
        var byPlate = SaleQueries.Table(new SaleTableInput { View = "awaitingDeposit", Registration = row.Registration })!;
        Assert.Single((await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, byPlate, default)).Value!.Items);
        var byOtherPlate = SaleQueries.Table(new SaleTableInput { View = "awaitingDeposit", Registration = "ZZZ 0000" })!;
        Assert.Empty((await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, byOtherPlate, default)).Value!.Items);

        var search = SaleQueries.Table(new SaleTableInput { View = "awaitingDeposit", Search = "no such customer" })!;
        var none = (await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, search, default)).Value!;
        Assert.Empty(none.Items);

        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Client.PostAsJsonAsync(
            $"/api/reservation-requests/{deposit.Id}/cancellations", new { })).StatusCode);
        var after = (await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, query, default)).Value!;
        Assert.Equal(0, after.Counts.AwaitingDeposit);
        Assert.Empty(after.Items);
    }
}
