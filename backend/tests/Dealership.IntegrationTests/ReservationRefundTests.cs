using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;
using static Dealership.IntegrationTests.DepositPaymentSupport;

namespace Dealership.IntegrationTests;

public sealed class ReservationRefundTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task APaidDepositIsRefundedToTheCustomersBankOnceItsReservationIsReleased()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        var payouts = new FakePayoutGateway(Key);
        using var paying = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(gateway);
            services.RemoveAll<IPayoutGateway>();
            services.AddSingleton<IPayoutGateway>(payouts);
        }));
        using var client = paying.CreateClient();
        await LoginAsync(client, scenario.Data.CustomerEmail);

        // Pay for the reservation through the signed return, then nothing is refundable while it stands.
        Assert.Equal(HttpStatusCode.OK, (await Post(client, scenario.ReservePath, scenario.Request,
            scenario.Data.OwnerId, assisted: false)).StatusCode);
        var bill = gateway.Bills.Single();
        await client.PostAsJsonAsync("/api/payments/returns", new { fields = gateway.Return(bill, true) });
        var paymentId = scenario.Request.OperationId;
        var before = await client.GetFromJsonAsync<RefundList>("/api/reservation-refunds");
        Assert.True(before!.Enabled);
        Assert.Contains(before.Banks, bank => bank.Code == "MBBEMYKL");
        Assert.Empty(before.Refunds);
        Assert.Equal(HttpStatusCode.NotFound, (await Account(client, paymentId, "MBBEMYKL")).StatusCode);

        // Cancelling releases the reservation, which makes the deposit refundable.
        var cancelled = await client.PostAsJsonAsync($"/api/reservations/{paymentId}/cancellations",
            new ReservationCancellationRequest(Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var due = (await client.GetFromJsonAsync<RefundList>("/api/reservation-refunds"))!.Refunds.Single();
        Assert.Equal("due", due.State);
        Assert.Equal(50000, due.AmountSen);
        Assert.Equal(paymentId, due.PaymentRequestId);

        // Bad details never reach the provider; a provider outage leaves the refund to ask again.
        Assert.Equal(HttpStatusCode.BadRequest, (await Account(client, paymentId, "NOTABANK")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Account(client, paymentId, "MBBEMYKL", "12ab")).StatusCode);
        Assert.Empty(payouts.Payouts);
        payouts.FailNext = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Account(client, paymentId, "MBBEMYKL")).StatusCode);
        Assert.Equal("failed", (await client.GetFromJsonAsync<RefundList>("/api/reservation-refunds"))!.Refunds.Single().State);

        // The retry pays out once with a fresh reference, and the account number is never stored.
        var requested = await Account(client, paymentId, "MBBEMYKL");
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var payout = payouts.Payouts.Single();
        Assert.Equal(50000, payout.AmountSen);
        Assert.Equal($"{paymentId:N}-2", payout.Reference);
        Assert.Equal("1234567890", payout.AccountNumber);
        var stored = await scenario.Database.ReservationRefunds.AsNoTracking().SingleAsync();
        Assert.Equal(RefundState.Processing, stored.State);
        Assert.Equal("7890", stored.AccountLast4);
        Assert.Equal("po-1", stored.PayoutId);
        Assert.Equal(HttpStatusCode.OK, (await Account(client, paymentId, "MBBEMYKL")).StatusCode);
        Assert.Single(payouts.Payouts);

        // An unsigned callback changes nothing; the signed one completes the refund once.
        Assert.Equal(HttpStatusCode.BadRequest, (await Callback(client, payouts.Callback("po-1", "completed", signed: false))).StatusCode);
        Assert.Equal(RefundState.Processing, (await scenario.Database.ReservationRefunds.AsNoTracking().SingleAsync()).State);
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, payouts.Callback("po-1", "completed"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Callback(client, payouts.Callback("po-1", "completed"))).StatusCode);
        var done = await scenario.Database.ReservationRefunds.AsNoTracking().SingleAsync();
        Assert.Equal(RefundState.Completed, done.State);
        Assert.NotNull(done.SettledAt);
        Assert.Equal("completed", (await client.GetFromJsonAsync<RefundList>("/api/reservation-refunds"))!.Refunds.Single().State);

        // The completed refund queued exactly one notice, and it tells the customer where the money went.
        var notice = await scenario.Database.ReservationRefundMailOutbox.AsNoTracking().SingleAsync();
        Assert.Equal(paymentId, notice.RefundId);
        Assert.Null(notice.DeliveredAt);
        var vehicle = await scenario.Database.Vehicles.AsNoTracking().SingleAsync();
        var mail = ReservationRefundMailComposer.Completed(done, vehicle, scenario.Data.CustomerEmail);
        Assert.Equal(scenario.Data.CustomerEmail, mail.Recipient);
        Assert.Contains("RM 500.00", mail.Body);
        Assert.Contains("Maybank, account ending 7890", mail.Body);
        Assert.DoesNotContain("1234567890", mail.Body);
    }

    internal static Task<HttpResponseMessage> Account(HttpClient client, Guid paymentId, string bank, string number = "1234567890")
    {
        return client.PostAsJsonAsync($"/api/reservation-refunds/{paymentId}/bank-account",
            new BankAccountDetails(bank, number, "Nur Aina"));
    }

    internal static Task<HttpResponseMessage> Callback(HttpClient client, Dictionary<string, string> fields)
    {
        return client.PostAsync("/api/payments/payouts/callback", new FormUrlEncodedContent(fields));
    }

    private sealed record Bank(string Code, string Name);

    private sealed record RefundRow(Guid PaymentRequestId, int AmountSen, string State);

    private sealed record RefundList(bool Enabled, List<Bank> Banks, List<RefundRow> Refunds);
}
