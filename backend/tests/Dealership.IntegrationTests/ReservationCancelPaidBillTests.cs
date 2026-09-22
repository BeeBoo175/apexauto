using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationCancelPaidBillTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CancellingAfterTheProviderTookThePaymentKeepsTheReservationWithoutARefund()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var request = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        await LoginAsync(client, scenario.Data.StaffEmail);

        // The customer paid, but the provider's callback has not reached the API when staff cancel.
        gateway.PaidAtProvider.Add(gateway.Bills.Single());
        var cancelled = await client.PostAsJsonAsync($"/api/reservation-requests/{request.Id}/cancellations", new { });

        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Contains("deposit-already-paid", await cancelled.Content.ReadAsStringAsync());
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
        var confirmed = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        Assert.NotNull(confirmed.ConfirmedAt);
        Assert.Null(confirmed.CancelledAt);
        var payment = await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync();
        Assert.Equal(DepositPaymentState.Paid, payment.State);

        // The late callback is then a harmless repeat rather than an unmatched payment.
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(gateway.Bills.Single(), true, 50000))).StatusCode);
        Assert.Equal(DepositPaymentState.Paid,
            (await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync()).State);
    }
}