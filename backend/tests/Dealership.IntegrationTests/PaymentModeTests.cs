using System.Net;
using System.Net.Http.Json;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class PaymentModeTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task TheModeEndpointSaysWhetherDepositsGoThroughTheProvider()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);

        foreach (var enabled in new[] { false, true })
        {
            using var factory = Paying(scenario, new FakePaymentGateway(Key, enabled));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/payments/mode");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(enabled, (await response.Content.ReadFromJsonAsync<Mode>())!.Live);
        }
    }

    private sealed record Mode(bool Live);
}
