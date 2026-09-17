using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

// The provider states its time in its own offset (Billplz writes +0800). The first version stored that offset and
// PostgreSQL refused it, leaving a reservation whose request never confirmed. One scenario per class: each scenario
// provisions its own branch, so a class can build only one.
public sealed class ReservationDepositPaymentTimeTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task APaymentTimeInTheProvidersLocalOffsetStillConfirmsTheReservationAndTheRequest()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        var bill = gateway.Bills.Single();
        var paidAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));

        var paid = await client.PostAsJsonAsync("/api/payments/returns",
            new { fields = gateway.Return(bill, true, paidAt: paidAt) });

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("confirmed", (await paid.Content.ReadFromJsonAsync<PaymentStatus>())!.Status);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
        var request = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        Assert.NotNull(request.ConfirmedAt);
        Assert.Equal(TimeSpan.Zero, request.ConfirmedAt!.Value.Offset);
        Assert.Equal("confirmed", await StatusAsync(client, request.Id));
    }
}
