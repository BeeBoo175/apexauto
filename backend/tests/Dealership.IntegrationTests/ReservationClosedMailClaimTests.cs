using System.Net;
using Dealership.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Dealership.IntegrationTests.DepositPaymentSupport;
using static Dealership.IntegrationTests.ReservationDepositRequestTests;

namespace Dealership.IntegrationTests;

public sealed class ReservationClosedMailClaimTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AClosedOwnersUnmatchedPaymentNoticeIsNeverClaimed()
    {
        using var scenario = await ReservationDepositScenario.CreateAsync(fixture);
        var gateway = new FakePaymentGateway(Key);
        using var paying = Paying(scenario, gateway);
        using var client = paying.CreateClient();
        var token = await RequestAsync(scenario);
        Assert.Equal(HttpStatusCode.OK, (await Confirm(client, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Callback(client, gateway.Callback(gateway.Bills.Single(), true, 100))).StatusCode);
        await ClosedOwnerSql.CloseAsync(scenario);
        var transport = new RecordingMailTransport(fail: false);
        using var worker = new MailDeliveryWorker(paying.Services.GetRequiredService<IServiceScopeFactory>(), transport,
            new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);

        await worker.DeliverBatchAsync(CancellationToken.None);

        var notice = await scenario.Database.ReservationUnmatchedPaymentMailOutbox.AsNoTracking().SingleAsync();
        Assert.Null(notice.DeliveredAt);
        Assert.Equal(0, notice.DeliveryAttempts);
    }
}
