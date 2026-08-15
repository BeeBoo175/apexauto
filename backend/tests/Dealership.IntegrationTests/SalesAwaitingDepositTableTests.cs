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
        Assert.Empty(page.Items);
        var row = Assert.Single(page.DepositRequests!);
        Assert.Equal(deposit.Id, row.RequestId);
        Assert.Equal(scenario.Data.OwnerId, row.CustomerId);
        Assert.Equal(deposit.ExpiresAt, row.ExpiresAtUtc);
        Assert.Equal(1, page.Total);

        var search = SaleQueries.Table(new SaleTableInput { View = "awaitingDeposit", Search = "no such customer" })!;
        var none = (await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, search, default)).Value!;
        Assert.Empty(none.DepositRequests!);

        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Client.PostAsJsonAsync(
            $"/api/reservation-requests/{deposit.Id}/cancellations", new { })).StatusCode);
        var after = (await new SaleListStore(scenario.Database).TableAsync(scenario.ManagerId, query, default)).Value!;
        Assert.Equal(0, after.Counts.AwaitingDeposit);
        Assert.Empty(after.DepositRequests!);
    }
}
