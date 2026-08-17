using System.Net;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositPaymentMismatchTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task APaymentOfTheWrongAmountIsKeptForStaffAndNeverReserves()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(gateway.Bills.Single(), true, 100))).StatusCode);

        var payment = await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync();
        Assert.Equal(DepositPaymentState.PaidUnmatched, payment.State);
        Assert.False(await scenario.Database.Reservations.AnyAsync());
        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;
        Assert.Equal("conflict", await StatusAsync(client, requestId));
        Assert.Equal(HttpStatusCode.Conflict, (await Confirm(client, token)).StatusCode);
    }
}
