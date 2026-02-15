using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AppointmentCancellationSnapshotTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task StaleRepeatableReadCannotAllocateAnAlreadyCommittedSlot()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var request = await BookingTestData.RequestAsync(factory, client);
        var owner = await BookingTestData.OwnerAsync(factory, registration);
        using (var scope = factory.Services.CreateScope())
        {
            var branch = Dealership.Infrastructure.Development.DevelopmentFixtureManifest.Branches[0] with
            {
                Id = Guid.NewGuid(),
                Name = "ApexAuto Subang Jaya"
            };
            await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>()
                .ProvisionBranchAsync(branch, CancellationToken.None);
            request = request with { BranchId = branch.Id };
        }

        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await using (var snapshot = new NpgsqlCommand("SELECT count(*) FROM \"IntakeAppointments\"", connection, transaction))
        {
            await snapshot.ExecuteScalarAsync();
        }

        var receipt = await BookingTestData.BookAsync(client, request);
        await using var duplicate = new NpgsqlCommand("""
            INSERT INTO "IntakeAppointments" ("Id", "OwnerId", "BranchId", "RegistrationPlate", "StartsAt", "EndsAt", "CreatedAt",
                "ValuationMake", "ValuationModel", "ValuationTrim", "ValuationManufactureYear", "ValuationMileageKilometres",
                "ValuationMinimum", "ValuationMaximum", "ValuationCurrency", "ValuationYear")
            VALUES (@id, @owner, @branch, 'BPL 2198', @start, @end, @created,
                @make, @model, @trim, @year, @mileage, @minimum, @maximum, @currency, @valuationYear)
            """, connection, transaction);
        duplicate.Parameters.AddWithValue("id", Guid.NewGuid());
        duplicate.Parameters.AddWithValue("owner", owner);
        duplicate.Parameters.AddWithValue("branch", receipt.BranchId);
        duplicate.Parameters.AddWithValue("start", receipt.Slot.StartsAt);
        duplicate.Parameters.AddWithValue("end", receipt.Slot.EndsAt);
        duplicate.Parameters.AddWithValue("created", receipt.CreatedAt);
        duplicate.Parameters.AddWithValue("make", receipt.Vehicle.Make!);
        duplicate.Parameters.AddWithValue("model", receipt.Vehicle.Model!);
        duplicate.Parameters.AddWithValue("trim", receipt.Vehicle.Trim!);
        duplicate.Parameters.AddWithValue("year", receipt.Vehicle.ManufactureYear!.Value);
        duplicate.Parameters.AddWithValue("mileage", receipt.Vehicle.MileageKilometres!.Value);
        duplicate.Parameters.AddWithValue("minimum", receipt.Quote.MinValuation);
        duplicate.Parameters.AddWithValue("maximum", receipt.Quote.MaxValuation);
        duplicate.Parameters.AddWithValue("currency", receipt.Quote.Currency);
        duplicate.Parameters.AddWithValue("valuationYear", receipt.Quote.ValuationYear);
        var error = await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }
}

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
            Assert.Single(await database.IntakeAppointments.Where(item => item.CancelledAt != null).ToArrayAsync());
            Assert.Single(await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Cancellation).ToArrayAsync());
            var source = new AppointmentMailSource(database, TimeProvider.System);
            var claims = new[]
            {
                (await source.ClaimNextAsync(CancellationToken.None))!,
                (await source.ClaimNextAsync(CancellationToken.None))!
            };
            var confirmation = Assert.Single(claims, claim => claim.Message.Subject == "ApexAuto inspection booking confirmed");
            Assert.Contains(replacement.Id.ToString(), confirmation.Message.Body);
            await source.MarkDeliveredAsync(confirmation.Id, confirmation.ClaimId, CancellationToken.None);
            var first = Assert.Single(claims, claim => claim.Message.Subject == "ApexAuto inspection booking cancelled");
            Assert.Null(await source.ClaimNextAsync(CancellationToken.None));
            var suppressed = await database.AppointmentMailOutbox.SingleAsync(item =>
                item.AppointmentId == receipt.Id && item.Kind == AppointmentMailKind.Confirmation);
            Assert.Equal(0, suppressed.DeliveryAttempts);
            Assert.Contains(receipt.Id.ToString(), first.Message.Body);
            Assert.DoesNotContain(registration.Nric!, first.Message.Body);
            var restarted = new AppointmentMailSource(database, new FixedClock(DateTimeOffset.UtcNow.AddMinutes(2)));
            var retry = (await restarted.ClaimNextAsync(CancellationToken.None))!;
            Assert.NotEqual(first.ClaimId, retry.ClaimId);
            await restarted.MarkDeliveredAsync(retry.Id, first.ClaimId, CancellationToken.None);
            Assert.Null((await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Cancellation).AsNoTracking().SingleAsync()).DeliveredAt);
            await restarted.MarkDeliveredAsync(retry.Id, retry.ClaimId, CancellationToken.None);
            Assert.NotNull((await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Cancellation).AsNoTracking().SingleAsync()).DeliveredAt);
        }

        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var immutable = new NpgsqlCommand("UPDATE \"IntakeAppointments\" SET \"CancelledAt\" = \"CancelledAt\" + interval '1 second' WHERE \"Id\" = @id", connection);
        immutable.Parameters.AddWithValue("id", receipt.Id);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync())).SqlState);
        await using var missingMail = new NpgsqlCommand("""
            UPDATE "IntakeAppointments" SET "CancelledAt" = clock_timestamp(),
                "CancellationPhotosExpireAt" = clock_timestamp() + interval '30 days' WHERE "Id" = @id
            """, connection);
        missingMail.Parameters.AddWithValue("id", replacement.Id);
        missingMail.Parameters.AddWithValue("owner", owner);
        var error = await Assert.ThrowsAsync<PostgresException>(() => missingMail.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_CancellationMail", error.ConstraintName);

        await CancellationPersistenceAssertions.VerifyAsync(connection, receipt.Id, replacement.Id);

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
