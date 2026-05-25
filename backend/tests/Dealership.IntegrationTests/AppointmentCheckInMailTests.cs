using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dealership.IntegrationTests;

public sealed class AppointmentCheckInMailTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task WorkerRetainsFailedDeliveryAndRestartFencesCompetingAndStaleClaims()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await data.ClaimAsync(appointment.Id);
        var signal = data.Factory.Services.GetRequiredService<MailDeliverySignal>();
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        using (var scope = data.Factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
                .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
            Assert.Equal(CheckInStatus.Succeeded, result.Status);
        }

        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        var clock = new DeliveryClock();
        using var firstHost = fixture.CreateFactory(clock: clock);
        var failedTransport = new RecordingMailTransport(true, data.CustomerEmail, "ApexAuto vehicle check-in confirmed");
        await Worker(firstHost, failedTransport).DeliverBatchAsync(CancellationToken.None);
        var originalMessage = await failedTransport.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(data.CustomerEmail, originalMessage.Recipient);
        Assert.Contains(appointment.Id.ToString(), originalMessage.Body);
        Assert.Contains(appointment.RegistrationPlate, originalMessage.Body);
        Assert.DoesNotContain(CheckInTestData.Evidence.Notes!, originalMessage.Body);
        var failed = await IntentAsync(firstHost);
        Assert.Null(failed.DeliveredAt);
        Assert.Equal(1, failed.DeliveryAttempts);
        Assert.NotNull(failed.DeliveryClaimId);

        clock.Now += TimeSpan.FromMinutes(2);
        using var restarted = fixture.CreateFactory(clock: clock);
        var competing = await Task.WhenAll(ClaimAsync(restarted), ClaimAsync(restarted));
        var retry = Assert.Single(competing, claim => claim is not null)!;
        Assert.Single(competing, claim => claim is null);
        Assert.Equal(originalMessage, retry.Message);
        Assert.NotEqual(failed.DeliveryClaimId, retry.ClaimId);
        await AcknowledgeAsync(restarted, failed.Id, failed.DeliveryClaimId!.Value);
        Assert.Null((await IntentAsync(restarted)).DeliveredAt);

        clock.Now += TimeSpan.FromMinutes(3);
        var successfulTransport = new RecordingMailTransport(false, data.CustomerEmail, originalMessage.Subject);
        await Worker(restarted, successfulTransport).DeliverBatchAsync(CancellationToken.None);
        Assert.Equal(originalMessage, await successfulTransport.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        var delivered = await IntentAsync(restarted);
        Assert.Equal(clock.Now, delivered.DeliveredAt);
        Assert.Equal(3, delivered.DeliveryAttempts);
        await AcknowledgeAsync(restarted, retry.Id, retry.ClaimId);
        Assert.Equal(delivered.DeliveredAt, (await IntentAsync(restarted)).DeliveredAt);
        Assert.Null(await ClaimAsync(restarted));
        using var finalScope = restarted.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Single(await database.Vehicles.ToArrayAsync());
        Assert.Single(await database.AppointmentCheckIns.ToArrayAsync());
    }

    private static MailDeliveryWorker Worker(WebApplicationFactory<Program> factory, IMailTransport transport)
    {
        return new MailDeliveryWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), transport,
            new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);
    }

    private static async Task<ClaimedMail?> ClaimAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return await Source(scope.ServiceProvider).ClaimNextAsync(CancellationToken.None);
    }

    private static async Task AcknowledgeAsync(WebApplicationFactory<Program> factory, Guid id, Guid claimId)
    {
        using var scope = factory.Services.CreateScope();
        await Source(scope.ServiceProvider).MarkDeliveredAsync(id, claimId, CancellationToken.None);
    }

    private static AppointmentMailSource Source(IServiceProvider services)
    {
        return services.GetServices<IMailDeliverySource>().OfType<AppointmentMailSource>().Single();
    }

    private static async Task<AppointmentMail> IntentAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DealershipDbContext>()
            .AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.CheckIn).AsNoTracking().SingleAsync();
    }

    private sealed class DeliveryClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
