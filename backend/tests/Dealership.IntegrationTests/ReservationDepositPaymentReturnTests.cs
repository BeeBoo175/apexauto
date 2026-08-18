using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositPaymentReturnTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task TheSignedReturnFromThePaymentPageConfirmsAtOnceWithoutWaitingForTheCallback()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var bill = gateway.Bills.Single();

        // Unsigned, unpaid and unknown returns confirm nothing.
        Assert.Equal(HttpStatusCode.BadRequest, (await Return(client, gateway.Return(bill, true, signed: false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Return(client, gateway.Return("nobill00000000000", true))).StatusCode);
        var unpaid = await Return(client, gateway.Return(bill, false));
        Assert.Equal("waiting", (await unpaid.Content.ReadFromJsonAsync<PaymentStatus>())!.Status);
        Assert.False(await scenario.Database.Reservations.AnyAsync());

        // The signed paid return reserves once; a repeat or the later callback records nothing new.
        var paid = await Return(client, gateway.Return(bill, true));
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("confirmed", (await paid.Content.ReadFromJsonAsync<PaymentStatus>())!.Status);
        Assert.Equal(HttpStatusCode.OK, (await Return(client, gateway.Return(bill, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/payments/callback",
            new FormUrlEncodedContent(gateway.Callback(bill, true, 50000)))).StatusCode);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
        var payment = await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync();
        Assert.Equal(DepositPaymentState.Paid, payment.State);
        Assert.Equal(HttpStatusCode.NoContent, (await Confirm(client, token)).StatusCode);
    }

    private static Task<HttpResponseMessage> Return(HttpClient client, Dictionary<string, string> fields)
    {
        return client.PostAsJsonAsync("/api/payments/returns", new { fields });
    }
}
