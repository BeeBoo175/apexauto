using System.Net;
using System.Net.Http.Json;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationOwnPaymentTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ACustomerPaysForTheirOwnReservationWithinAShortHoldAndTheSignedReturnReserves()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);

        // Reserving opens a bill and a short hold; nothing is reserved or mailed until the payment is confirmed.
        var started = await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var address = (await started.Content.ReadFromJsonAsync<PaymentAddress>())!.PaymentUrl;
        var bill = gateway.Bills.Single();
        Assert.EndsWith(bill, address);
        var held = await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync();
        Assert.Null(held.BookedByStaffId);
        Assert.Equal(held.CreatedAt.AddMinutes(15), held.ExpiresAt);
        Assert.False(await scenario.Database.Reservations.AnyAsync());
        Assert.False(await scenario.Database.ReservationDepositMailOutbox.AnyAsync());

        // The same operation replays the same bill; another customer cannot reserve the held car.
        var repeated = await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        Assert.Equal(address, (await repeated.Content.ReadFromJsonAsync<PaymentAddress>())!.PaymentUrl);
        Assert.Single(gateway.Bills);
        var other = await scenario.RegisterOtherCustomerAsync();
        await LoginAsync(scenario.Client, scenario.Data.StaffEmail);
        var rival = await Post(scenario.Client, scenario.ReservePath, scenario.Request with { OperationId = Guid.NewGuid() },
            other, assisted: true);
        Assert.Equal(HttpStatusCode.Conflict, rival.StatusCode);

        var paid = await client.PostAsJsonAsync("/api/payments/billplz/returns", new { fields = gateway.Return(bill, true) });
        Assert.Equal("confirmed", (await paid.Content.ReadFromJsonAsync<PaymentStatus>())!.Status);
        var reservation = await scenario.Database.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(held.Id, reservation.Id);
        Assert.Equal(scenario.Data.OwnerId, reservation.CustomerId);
        Assert.Null(reservation.BookedByStaffId);

        // A replay after payment returns the same receipt instead of a second bill or reservation.
        var replay = await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Single(gateway.Bills);
        Assert.Equal(1, await scenario.Database.Reservations.CountAsync());
    }
}
