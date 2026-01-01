using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AppointmentCancellationTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CancellationIsOwnedAtomicReplayableAndReleasesOnlyItsSlotWhileKeepingBookingEvidence()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var request = await BookingTestData.RequestAsync(factory, client);
        var receipt = await BookingTestData.BookAsync(client, request);
        using var other = factory.CreateClient();
        var outsider = await IntakePhotoFixture.RegisterAsync(other);
        await IntakePhotoFixture.VerifyAsync(factory, other, outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/intake/appointments/{receipt.Id}/cancellation", null)).StatusCode);
        var responses = await Task.WhenAll(client.PostAsync($"/api/intake/appointments/{receipt.Id}/cancellation", null),
            client.PostAsync($"/api/intake/appointments/{receipt.Id}/cancellation", null));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var cancelled = (await responses[0].Content.ReadFromJsonAsync<CancellationReceipt>())!;
        Assert.Equal(cancelled, await responses[1].Content.ReadFromJsonAsync<CancellationReceipt>());
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(
            await client.GetFromJsonAsync<AppointmentReceipt>($"/api/intake/appointments/{receipt.Id}")));
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(await BookingTestData.BookAsync(client, request)));
        var history = (await client.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments"))!;
        Assert.Equal(cancelled.CancelledAt, Assert.Single(history.Items).CancelledAt);
        var date = DateOnly.FromDateTime(request.StartsAt.AddHours(8).DateTime);
        var offered = (await client.GetFromJsonAsync<InspectionSlotOffer>($"/api/branches/{request.BranchId}/inspection-slots?date={date:yyyy-MM-dd}"))!;
        Assert.Contains(offered.Slots, slot => slot.StartsAt == request.StartsAt);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/intake/appointments", request with { Id = Guid.NewGuid() })).StatusCode);
        var fresh = request with { Id = Guid.NewGuid(), PhotoIds = await BookingTestData.PhotosAsync(client) };
        var replacement = await BookingTestData.BookAsync(client, fresh);
        Assert.NotEqual(receipt.Id, replacement.Id);
        var owner = await BookingTestData.OwnerAsync(factory, registration);

        using (var late = fixture.CreateFactory(clock: new FixedClock(request.StartsAt)))
        using (var scope = late.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAppointmentCancellationStore>();
            Assert.Equal(CancellationStatus.TooLate, (await store.CancelAsync(owner, replacement.Id, CancellationToken.None)).Status);
            Assert.Equal(cancelled, (await store.CancelAsync(owner, receipt.Id, CancellationToken.None)).Receipt);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Single(await database.AppointmentCancellations.ToArrayAsync());
            Assert.Single(await database.AppointmentCancellationMails.ToArrayAsync());
            var confirmations = new AppointmentConfirmationSource(database, TimeProvider.System);
            Assert.Equal(replacement.Id, (await confirmations.ClaimNextAsync(CancellationToken.None))!.Id);
            Assert.Null(await confirmations.ClaimNextAsync(CancellationToken.None));
            var source = new AppointmentCancellationMailSource(database, TimeProvider.System);
            var first = (await source.ClaimNextAsync(CancellationToken.None))!;
            Assert.Contains(receipt.Id.ToString(), first.Message.Body);
            Assert.DoesNotContain(registration.Nric!, first.Message.Body);
            var restarted = new AppointmentCancellationMailSource(database, new FixedClock(DateTimeOffset.UtcNow.AddMinutes(2)));
            var retry = (await restarted.ClaimNextAsync(CancellationToken.None))!;
            Assert.NotEqual(first.ClaimId, retry.ClaimId);
            await restarted.MarkDeliveredAsync(retry.Id, first.ClaimId, CancellationToken.None);
            Assert.Null((await database.AppointmentCancellationMails.AsNoTracking().SingleAsync()).DeliveredAt);
            await restarted.MarkDeliveredAsync(retry.Id, retry.ClaimId, CancellationToken.None);
            Assert.NotNull((await database.AppointmentCancellationMails.AsNoTracking().SingleAsync()).DeliveredAt);
        }

        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var immutable = new NpgsqlCommand("UPDATE \"AppointmentCancellations\" SET \"CancelledAt\" = \"CancelledAt\" + interval '1 second' WHERE \"AppointmentId\" = @id", connection);
        immutable.Parameters.AddWithValue("id", receipt.Id);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync())).SqlState);
        await using var missingMail = new NpgsqlCommand("""
            INSERT INTO "AppointmentCancellations" ("AppointmentId", "OwnerId", "CancelledAt", "PhotosExpireAt")
            VALUES (@id, @owner, clock_timestamp(), clock_timestamp() + interval '30 days')
            """, connection);
        missingMail.Parameters.AddWithValue("id", replacement.Id);
        missingMail.Parameters.AddWithValue("owner", owner);
        var error = await Assert.ThrowsAsync<PostgresException>(() => missingMail.ExecuteNonQueryAsync());
        Assert.Equal("CK_AppointmentCancellations_Mail", error.ConstraintName);

        using var restartedFactory = fixture.CreateFactory();
        using var restartedClient = restartedFactory.CreateClient();
        await IntakePhotoFixture.LoginAsync(restartedClient, registration);
        Assert.Equal(cancelled, await (await restartedClient.PostAsync($"/api/intake/appointments/{receipt.Id}/cancellation", null))
            .Content.ReadFromJsonAsync<CancellationReceipt>());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
