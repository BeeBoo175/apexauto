using System.Net;
using System.Net.Http.Json;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationPendingPaymentAccountTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AnOpenPaymentShowsOnTheAccountAndTheCustomerCanGiveItUp()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);

        var started = await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var requestId = (await scenario.Database.ReservationDepositRequests.AsNoTracking().SingleAsync()).Id;

        // The unpaid bill is visible to the customer, who has no reservation yet.
        var account = await new ReservationReadStore(scenario.Database)
            .AccountAsync(scenario.Data.OwnerId, 1, CancellationToken.None);
        Assert.Equal(requestId, account.History!.PendingPayment!.RequestId);
        Assert.Null(account.History.Active);

        // Continuing goes to the provider's bill, because the public vehicle page hides a car with a pending request.
        var bill = await scenario.Database.ReservationDepositPayments.AsNoTracking()
            .SingleAsync(payment => payment.RequestId == requestId);
        Assert.Equal(bill.BillUrl, account.History.PendingPayment.PaymentUrl);
        Assert.False(account.History.PendingPayment.BookedByStaff);

        // The customer can give it up, and the car is free again.
        var cancelled = await client.PostAsync($"/api/account/pending-payments/{requestId}/cancellations", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var after = await new ReservationReadStore(scenario.Database)
            .AccountAsync(scenario.Data.OwnerId, 1, CancellationToken.None);
        Assert.Null(after.History!.PendingPayment);
    }
}