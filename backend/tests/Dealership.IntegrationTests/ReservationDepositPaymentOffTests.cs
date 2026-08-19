using System.Net;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositPaymentOffTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task WithTheGatewayOffTheLinkKeepsConfirmingTheSimulatedDeposit()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        // The gateway is off whatever the machine's own settings say, so the test never depends on a local .env.
        using var factory = Paying(scenario, new FakePaymentGateway(Key, enabled: false));
        using var client = factory.CreateClient();
        var token = await RequestAsync(scenario);

        Assert.Equal(HttpStatusCode.NoContent, (await Confirm(client, token)).StatusCode);
        Assert.False(await scenario.Database.ReservationDepositPayments.AnyAsync());
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }
}
