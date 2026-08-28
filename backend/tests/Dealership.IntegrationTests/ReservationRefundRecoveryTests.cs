using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationRefundRecoveryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ParallelRetriesAskForOnePayoutAndATimedOutPayoutSettlesFromTheProvidersNotice()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var payouts = new FakePayoutGateway(Key);
        using var paying = await PayAndReleaseAsync(scenario, payouts);
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);
        var paymentId = scenario.Request.OperationId;
        payouts.FailNext = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await ReservationRefundTests.Account(client, paymentId, "MBBEMYKL")).StatusCode);

        // Two parallel retries of the failed refund reach the provider once, and that request times out.
        payouts.TimeOutNext = true;
        var retries = await Task.WhenAll(ReservationRefundTests.Account(client, paymentId, "MBBEMYKL"), ReservationRefundTests.Account(client, paymentId, "MBBEMYKL"));
        Assert.All(retries, retry => Assert.Equal(HttpStatusCode.OK, retry.StatusCode));
        Assert.Single(payouts.Payouts);

        // The payout may already have been made, so the refund stays processing and asking again sends nothing.
        Assert.Equal(HttpStatusCode.OK, (await ReservationRefundTests.Account(client, paymentId, "MBBEMYKL")).StatusCode);
        Assert.Single(payouts.Payouts);
        var waiting = await scenario.Database.ReservationRefunds.AsNoTracking().SingleAsync();
        Assert.Equal(RefundState.Processing, waiting.State);
        Assert.Null(waiting.PayoutId);

        // The notice carries the attempt's reference, which finds the refund without a saved payout id.
        var callback = payouts.Callback("po-9", "completed", reference: waiting.Reference);
        Assert.Equal(HttpStatusCode.OK, (await ReservationRefundTests.Callback(client, callback)).StatusCode);
        var done = await scenario.Database.ReservationRefunds.AsNoTracking().SingleAsync();
        Assert.Equal(RefundState.Completed, done.State);
        Assert.Equal("po-9", done.PayoutId);
    }

    private static async Task<WebApplicationFactory<Program>> PayAndReleaseAsync(ReservationDepositScenario scenario,
        FakePayoutGateway payouts)
    {
        var gateway = new FakePaymentGateway(Key);
        var paying = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(gateway);
            services.RemoveAll<IPayoutGateway>();
            services.AddSingleton<IPayoutGateway>(payouts);
        }));
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);
        await Post(client, scenario.ReservePath, scenario.Request, scenario.Data.OwnerId, assisted: false);
        await client.PostAsJsonAsync("/api/payments/returns", new { fields = gateway.Return(gateway.Bills.Single(), true) });
        await client.PostAsJsonAsync($"/api/reservations/{scenario.Request.OperationId}/cancellations",
            new ReservationCancellationRequest(Guid.NewGuid(), null));
        return paying;
    }
}
