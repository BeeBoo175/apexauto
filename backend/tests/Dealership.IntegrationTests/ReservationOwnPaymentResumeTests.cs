using System.Net;
using System.Net.Http.Json;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationOwnPaymentResumeTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ACustomerWhoReturnsToTheSameCarMidPaymentResumesTheOpenBillInsteadOfBeingRefused()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);

        var started = await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var address = (await started.Content.ReadFromJsonAsync<PaymentAddress>())!.PaymentUrl;

        // Back from the payment page, or after leaving it unopened, Reserve is pressed again with a fresh operation ID.
        // The customer's own open request for this car is picked up again, not refused as a second reservation.
        var again = await Post(client, scenario.ReservePath, scenario.Request with { OperationId = Guid.NewGuid() },
            scenario.Data.OwnerId, assisted: false);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(address, (await again.Content.ReadFromJsonAsync<PaymentAddress>())!.PaymentUrl);
        Assert.Single(gateway.Bills);
        Assert.Equal(1, await scenario.Database.ReservationDepositRequests.CountAsync());
    }
}
