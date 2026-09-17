using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

// Whatever order the browser return and the provider callback arrive in, and however often each repeats, the
// customer ends with exactly one reservation and a confirmed request.
public sealed class ReservationDepositPaymentReplayTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RepeatedAndReorderedPaymentNoticesConvergeOnOneConfirmedReservation()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var bill = gateway.Bills.Single();
        var paidAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));

        Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(bill, true, 50000))).StatusCode);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var returned = await client.PostAsJsonAsync("/api/payments/returns",
                new { fields = gateway.Return(bill, true, paidAt: paidAt) });
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Callback(client, gateway.Callback(bill, true, 50000))).StatusCode);
        }

        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
        var request = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        Assert.NotNull(request.ConfirmedAt);
        Assert.Equal(DepositPaymentState.Paid,
            (await scenario.Database.ReservationDepositPayments.AsNoTracking().SingleAsync()).State);
    }
}
