using System.Net;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationStaffPendingPaymentAccountTests(IntakePhotoFixture fixture)
    : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AStaffMadeRequestShowsOnTheCustomersAccountAsBookedByStaff()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        var requested = await Post(scenario.Client, scenario.RequestPath, scenario.Request, scenario.Data.OwnerId);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;

        var account = await new ReservationReadStore(scenario.Database)
            .AccountAsync(scenario.Data.OwnerId, 1, CancellationToken.None);

        Assert.Equal(requestId, account.History!.PendingPayment!.RequestId);
        Assert.True(account.History.PendingPayment.BookedByStaff);
    }
}
