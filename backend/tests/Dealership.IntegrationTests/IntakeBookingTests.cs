using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dealership.IntegrationTests;

public sealed class IntakeBookingTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task BookingRechecksQuotePhotosAndSlotsAndPreservesReceiptsEvidenceAndMailAcrossRestart()
    {
        Guid owner;
        Guid otherOwner;
        Guid oldClaim;
        Guid messageId;
        AppointmentReceipt receipt;
        BookingRequest request;
        Dealership.Application.Accounts.RegistrationRequest registration;

        using (var factory = fixture.CreateFactory())
        using (var client = factory.CreateClient())
        {
            registration = await IntakePhotoFixture.RegisterAsync(client);
            var invalid = new BookingRequest(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1),
                "WYY 8821", new ValuationRequest("Proton", "Saga", "Standard", 2020, 90000),
                new ValuationResponse(1, 2, "MYR", 2026), [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/intake/appointments", invalid)).StatusCode);
            await IntakePhotoFixture.VerifyAsync(factory, client, registration);
            owner = await BookingTestData.OwnerAsync(factory, registration);
            request = await BookingTestData.RequestAsync(factory, client);
            using var other = factory.CreateClient();
            var otherRegistration = await IntakePhotoFixture.RegisterAsync(other);
            await IntakePhotoFixture.VerifyAsync(factory, other, otherRegistration);
            otherOwner = await BookingTestData.OwnerAsync(factory, otherRegistration);

            var changed = request.AcceptedQuote! with { MaxValuation = request.AcceptedQuote!.MaxValuation + 1 };
            var response = await client.PostAsJsonAsync("/api/intake/appointments", request with { AcceptedQuote = changed });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(request.AcceptedQuote, await response.Content.ReadFromJsonAsync<ValuationResponse>());

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/intake/appointments",
                request with { PhotoIds = request.PhotoIds!.Take(3).ToArray() })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/intake/appointments", request)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
                Assert.False(await database.IntakeAppointments.AnyAsync());
                Assert.False(await database.SellerValuations.AnyAsync());
                Assert.False(await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Confirmation).AnyAsync());
            }

            var competing = await Task.WhenAll(BookingTestData.BookAsync(client, request), BookingTestData.BookAsync(client, request));
            receipt = competing[0];
            Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(competing[1]));
            Assert.Equal(request.AcceptedQuote, receipt.Quote);
            Assert.Equal(4, receipt.Photos.Count);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/intake/appointments/{receipt.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/intake/appointments", request)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/intake/appointments",
                request with { RegistrationPlate = "BPP 1234" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/intake/appointments",
                request with { Id = Guid.NewGuid(), StartsAt = request.StartsAt.AddMinutes(90) })).StatusCode);
            var date = DateOnly.FromDateTime(request.StartsAt.AddHours(8).DateTime);
            var offer = await client.GetFromJsonAsync<InspectionSlotOffer>($"/api/branches/{request.BranchId}/inspection-slots?date={date:yyyy-MM-dd}");
            Assert.DoesNotContain(offer!.Slots, slot => slot.StartsAt == request.StartsAt);

            var worker = new MailDeliveryWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
                new FailedMailTransport(), NullLogger<MailDeliveryWorker>.Instance);
            await worker.DeliverBatchAsync(CancellationToken.None);
            using var mailScope = factory.Services.CreateScope();
            var message = await mailScope.ServiceProvider.GetRequiredService<DealershipDbContext>()
                .AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Confirmation).AsNoTracking().SingleAsync();
            Assert.Null(message.DeliveredAt);
            Assert.Equal(1, message.DeliveryAttempts);
            oldClaim = message.DeliveryClaimId!.Value;
            messageId = message.Id;
        }

        using var restarted = fixture.CreateFactory();
        using var restartedClient = restarted.CreateClient();
        await IntakePhotoFixture.LoginAsync(restartedClient, registration);
        var replay = await BookingTestData.BookAsync(restartedClient, request);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(replay));
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(
            await restartedClient.GetFromJsonAsync<AppointmentReceipt>($"/api/intake/appointments/{receipt.Id}")));
        using (var scope = restarted.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Single(await database.IntakeAppointments.ToArrayAsync());
            Assert.Single(await database.SellerValuations.ToArrayAsync());
            var source = new AppointmentMailSource(database, new LaterClock());
            var claimed = await source.ClaimNextAsync(CancellationToken.None);
            Assert.NotNull(claimed);
            Assert.Equal(messageId, claimed.Id);
            Assert.NotEqual(oldClaim, claimed.ClaimId);
            Assert.Contains(receipt.Id.ToString(), claimed.Message.Body);
            Assert.DoesNotContain(registration.Nric!, claimed.Message.Body);
            await source.MarkDeliveredAsync(claimed.Id, oldClaim, CancellationToken.None);
            Assert.Null((await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Confirmation).AsNoTracking().SingleAsync()).DeliveredAt);
            await source.MarkDeliveredAsync(claimed.Id, claimed.ClaimId, CancellationToken.None);
            Assert.NotNull((await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Confirmation).AsNoTracking().SingleAsync()).DeliveredAt);
        }

        var photosOne = await BookingTestData.PhotosAsync(restartedClient);
        var photosTwo = await BookingTestData.PhotosAsync(restartedClient);
        var raceOne = request with { Id = Guid.NewGuid(), StartsAt = request.StartsAt.AddMinutes(90), PhotoIds = photosOne };
        var raceTwo = raceOne with { Id = Guid.NewGuid(), PhotoIds = photosTwo };
        var slotRace = await Task.WhenAll(restartedClient.PostAsJsonAsync("/api/intake/appointments", raceOne),
            restartedClient.PostAsJsonAsync("/api/intake/appointments", raceTwo));
        Assert.Single(slotRace, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(slotRace, response => response.StatusCode == HttpStatusCode.Conflict);
        var unusedPhotos = slotRace[0].StatusCode == HttpStatusCode.OK ? photosTwo : photosOne;
        await BookingSqlAssertions.VerifyAsync(fixture.Database, receipt.Id, otherOwner, unusedPhotos);

        using var expired = fixture.CreateFactory(clock: new LaterClock(TimeSpan.FromHours(25)));
        using var expiredScope = expired.Services.CreateScope();
        var media = expiredScope.ServiceProvider.GetRequiredService<IntakePhotoService>();
        Assert.Equal(PhotoStatus.Available, (await media.GetReceiptAsync(owner, request.PhotoIds![0], CancellationToken.None)).Status);
        using var bytes = new MemoryStream();
        Assert.Equal(PhotoStatus.Available, (await media.DownloadAsync(owner, request.PhotoIds[0], bytes, CancellationToken.None)).Status);
        Assert.Equal(IntakePhotoFixture.Image(), bytes.ToArray());
    }

    private sealed class LaterClock(TimeSpan? advance = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + (advance ?? TimeSpan.FromMinutes(2));
    }

    private sealed class FailedMailTransport : IMailTransport
    {
        public bool Enabled => true;
        public void Validate()
        {
        }

        public Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
        {
            return Task.FromException(new IOException("Controlled test transport failure."));
        }
    }
}
