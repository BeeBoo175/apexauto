using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationDepositPaymentTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ThePaymentLinkStartsOneBillAndOnlyTheSignedCallbackMakesTheReservation()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);

        // The link opens a bill instead of confirming; repeating it returns the same one.
        var started = await Confirm(client, token);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var address = (await started.Content.ReadFromJsonAsync<PaymentAddress>())!.PaymentUrl;
        Assert.EndsWith(gateway.Bills.Single(), address);
        var repeated = await (await Confirm(client, token)).Content.ReadFromJsonAsync<PaymentAddress>();
        Assert.Equal(address, repeated!.PaymentUrl);
        Assert.Single(gateway.Bills);
        Assert.False(await scenario.Database.Reservations.AnyAsync());

        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;
        Assert.Equal("waiting", await StatusAsync(client, requestId));
        var bill = gateway.Bills.Single();

        // An unsigned or tampered delivery and an unpaid one change nothing.
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, gateway.Callback(bill, true, 50000, signed: false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(bill, false, 0))).StatusCode);
        Assert.False(await scenario.Database.Reservations.AnyAsync());

        // The full signed payment reserves once, however many times Billplz delivers it.
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(bill, true, 50000))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(bill, true, 50000))).StatusCode);
        var reservation = await scenario.Database.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(requestId, reservation.Id);
        var payment = await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync();
        Assert.Equal(DepositPaymentState.Paid, payment.State);
        Assert.Equal(50000, payment.PaidAmountSen);
        Assert.Equal("confirmed", await StatusAsync(client, requestId));
        Assert.Equal(HttpStatusCode.NoContent, (await Confirm(client, token)).StatusCode);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }
}
