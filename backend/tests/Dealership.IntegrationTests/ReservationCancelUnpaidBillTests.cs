using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationCancelUnpaidBillTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CancellingWhileTheBillIsUnpaidDeletesItAndReleasesTheCar()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var request = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        await LoginAsync(client, scenario.Data.StaffEmail);

        var cancelled = await client.PostAsJsonAsync($"/api/reservation-requests/{request.Id}/cancellations", new { });

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(gateway.Bills.Single(), gateway.Deleted.Single());
        var payment = await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync();
        Assert.NotNull(payment.BillDeletedAt);
        Assert.Equal(0, await scenario.Database.Reservations.CountAsync());
        Assert.NotNull((await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).CancelledAt);
    }
}
