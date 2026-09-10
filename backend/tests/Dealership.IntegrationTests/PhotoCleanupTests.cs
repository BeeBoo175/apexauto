using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Domain.Accounts;
using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SkiaSharp;

namespace Dealership.IntegrationTests;

public sealed class PhotoCleanupTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CleanupCoordinatesTransfersRetainsEvidenceAndReconcilesLostDeletionAcknowledgementsAfterRestart()
    {
        using var factory = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(10).TotalHours,
            cancelledRetentionDays: TimeSpan.FromHours(1).TotalDays);
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var request = await BookingTestData.RequestAsync(factory, client);
        var active = await BookingTestData.BookAsync(client, request);
        var cancelled = await BookingTestData.BookAsync(client, request with
        {
            Id = Guid.NewGuid(),
            StartsAt = request.StartsAt.AddMinutes(90),
            PhotoIds = await BookingTestData.PhotosAsync(client)
        });
        var signal = factory.Services.GetRequiredService<PhotoCleanupSignal>();
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/intake/appointments/{cancelled.Id}/cancellations", null)).StatusCode);
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await RunCleanupAsync(factory);
        Assert.True(await fixture.OwnedObjectExistsAsync(cancelled.Photos[0].Id));
        var unexpired = Guid.NewGuid();
        using (var longLifetime = fixture.CreateFactory(unsubmittedLifetimeHours: 24))
        using (var longClient = longLifetime.CreateClient())
        {
            longClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(longClient, unexpired, IntakePhotoFixture.Image())).StatusCode);
            await longLifetime.Services.GetRequiredService<PhotoCleanupSignal>().WaitAsync(CancellationToken.None)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        await VerifyPrematureSqlAsync(unexpired, cancelled.Photos[0].Id);
        var unsubmitted = Guid.NewGuid();
        using (var shortLifetime = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(2).TotalHours))
        using (var shortClient = shortLifetime.CreateClient())
        {
            shortClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(shortClient, unsubmitted, IntakePhotoFixture.Image())).StatusCode);
        }

        var pending = Guid.NewGuid();
        using (var uncertain = fixture.CreateFactory(failAfterPut: true, unsubmittedLifetimeHours: TimeSpan.FromSeconds(2).TotalHours))
        using (var uncertainClient = uncertain.CreateClient())
        {
            uncertainClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await IntakePhotoFixture.UploadAsync(uncertainClient, pending, IntakePhotoFixture.Image())).StatusCode);
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(pending));
        var owner = await BookingTestData.OwnerAsync(factory, registration);
        using (var scope = factory.Services.CreateScope())
        {
            var expires = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().IntakePhotos
                .Where(photo => photo.Id == pending || photo.Id == unsubmitted).MaxAsync(photo => photo.ExpiresAt);
            await WaitUntilAsync(expires);
        }

        using var failing = fixture.CreateFactory(failBeforeDelete: true);
        using (var transferScope = factory.Services.CreateScope())
        {
            await using var transfer = await transferScope.ServiceProvider.GetRequiredService<IPhotoTransferLock>()
                .TryAcquireAsync(unsubmitted, CancellationToken.None);
            Assert.NotNull(transfer);
            using var competingScope = factory.Services.CreateScope();
            Assert.Null(await competingScope.ServiceProvider.GetRequiredService<IPhotoTransferLock>()
                .TryAcquireAsync(unsubmitted, CancellationToken.None));
            await RunCleanupAsync(failing);
            Assert.True(await fixture.OwnedObjectExistsAsync(unsubmitted));
            Assert.True(await fixture.OwnedObjectExistsAsync(pending));
        }

        await ExpireClaimsAsync();
        using (var lostAcknowledgement = fixture.CreateFactory(failAfterDelete: true))
        {
            await RunCleanupAsync(lostAcknowledgement);
        }

        Assert.False(await fixture.OwnedObjectExistsAsync(pending));
        Assert.False(await fixture.OwnedObjectExistsAsync(unsubmitted));
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/api/intake/photos/{pending}")).StatusCode);
        var pendingReceipt = await (await client.GetAsync($"/api/intake/photos/{pending}/receipt")).Content.ReadFromJsonAsync<PhotoReceipt>();
        Assert.Equal("DeletionPending", pendingReceipt!.Status);
        Assert.Equal("UnsubmittedExpired", pendingReceipt.RemovalReason);
        Assert.Null(pendingReceipt.DeletedAt);
        Assert.Equal(HttpStatusCode.Gone, (await IntakePhotoFixture.UploadAsync(client, pending, IntakePhotoFixture.Image())).StatusCode);
        Guid oldClaim;
        using (var scope = factory.Services.CreateScope())
        {
            var removal = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().PhotoRemovals.AsNoTracking()
                .SingleAsync(item => item.PhotoId == pending);
            oldClaim = removal.ClaimId!.Value;
        }

        await ExpireClaimsAsync();
        using var restarted = fixture.CreateFactory();
        using (var scope = restarted.Services.CreateScope())
        {
            await using var transfer = await scope.ServiceProvider.GetRequiredService<IPhotoTransferLock>()
                .TryAcquireAsync(pending, CancellationToken.None);
            Assert.NotNull(transfer);
            var store = scope.ServiceProvider.GetRequiredService<PhotoCleanupStore>();
            var claim = (await store.ClaimAsync(pending, CancellationToken.None))!;
            Assert.NotEqual(oldClaim, claim.ClaimId);
            await store.MarkDeletedAsync(pending, oldClaim, CancellationToken.None);
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Null((await database.PhotoRemovals.AsNoTracking().SingleAsync(item => item.PhotoId == pending)).DeletedAt);
            await scope.ServiceProvider.GetRequiredService<IPhotoStorage>().DeleteAsync(claim.Photo, CancellationToken.None);
            await store.MarkDeletedAsync(pending, claim.ClaimId, CancellationToken.None);
        }

        await RunCleanupAsync(restarted);
        var deleted = await (await client.GetAsync($"/api/intake/photos/{pending}/receipt")).Content.ReadFromJsonAsync<PhotoReceipt>();
        Assert.Equal("Deleted", deleted!.Status);
        Assert.NotNull(deleted.DeletedAt);
        using (var scope = factory.Services.CreateScope())
        {
            await ExpireCancellationRetentionAsync(cancelled.Id);
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(cancelled.Photos[0].Id));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/intake/photos/{cancelled.Photos[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/intake/photos/{cancelled.Photos[0].Id}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/intake/photos/{active.Photos[0].Id}")).StatusCode);

        using (var afterRetention = fixture.CreateFactory())
        {
            await Task.WhenAll(RunCleanupAsync(afterRetention), RunCleanupAsync(afterRetention));
        }

        foreach (var photo in active.Photos)
        {
            Assert.True(await fixture.OwnedObjectExistsAsync(photo.Id));
        }

        foreach (var photo in cancelled.Photos)
        {
            Assert.False(await fixture.OwnedObjectExistsAsync(photo.Id));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            Assert.Equal(8, await database.AppointmentPhotos.CountAsync());
            Assert.Equal(6, await database.PhotoRemovals.CountAsync(item => item.DeletedAt != null));
            var service = scope.ServiceProvider.GetRequiredService<IntakePhotoService>();
            Assert.Equal(PhotoStatus.Deleted, (await service.GetReceiptAsync(owner, cancelled.Photos[0].Id, CancellationToken.None)).Status);
        }

        await VerifyRestrictedSqlAsync(active.Photos[0].Id, pending);
    }

    [Fact]
    public async Task AccountPictureCleanupSurvivesCrashAfterLogicalRemovalAndLostDeletionAcknowledgements()
    {
        var lifetime = TimeSpan.FromSeconds(2).TotalHours;
        using var factory = fixture.CreateFactory(pendingUploadLifetimeHours: lifetime);
        using var client = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(client);
        var ownerId = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        var replaced = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var abandoned = Guid.NewGuid();
        var current = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await PutPictureAsync(client, replaced, SKColors.SteelBlue)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutPictureAsync(client, removed, SKColors.OrangeRed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{removed}")).StatusCode);

        // The process "crashed" after the logical removals: nothing was deleted inline and the receipts are durable.
        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(replaced)));
        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(removed)));
        Assert.Equal("DeletionPending", await ReceiptStatusAsync(client, replaced, ownerId, removed));
        using (var outage = fixture.CreateFactory(failAfterPut: true, pendingUploadLifetimeHours: lifetime))
        using (var outageClient = outage.CreateClient())
        {
            outageClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await PutPictureAsync(outageClient, abandoned, SKColors.Green)).StatusCode);
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(abandoned)));
        Assert.Equal(HttpStatusCode.OK, (await PutPictureAsync(client, current, SKColors.Purple)).StatusCode);
        await WaitUntilAsync(await ExpiryAsync(abandoned));

        using (var failing = fixture.CreateFactory(failBeforeDelete: true))
        {
            await RunCleanupAsync(failing);
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(removed)));
        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(abandoned)));
        await ExpirePictureClaimsAsync();
        using (var lostAcknowledgement = fixture.CreateFactory(failAfterDelete: true))
        {
            await RunCleanupAsync(lostAcknowledgement);
        }

        foreach (var id in new[] { replaced, removed, abandoned })
        {
            Assert.False(await fixture.OwnedObjectExistsAsync(PictureKey(id)));
        }

        Assert.Equal("DeletionPending", await ReceiptStatusAsync(client, removed, ownerId, removed));
        await ExpirePictureClaimsAsync();
        using (var restarted = fixture.CreateFactory())
        {
            await RunCleanupAsync(restarted);
        }

        // The confirmed current picture is never eligible and its object and download survive every pass.
        Assert.True(await fixture.OwnedObjectExistsAsync(PictureKey(current)));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/accounts/{ownerId}/picture")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var removals = await database.AccountPictureRemovals.AsNoTracking().ToDictionaryAsync(item => item.PictureId);
            Assert.Equal(AccountPictureRemovalReason.Replaced, removals[replaced].Reason);
            Assert.Equal(AccountPictureRemovalReason.Removed, removals[removed].Reason);
            Assert.Equal(AccountPictureRemovalReason.UnconfirmedExpired, removals[abandoned].Reason);
            Assert.All(removals.Values, item => Assert.NotNull(item.DeletedAt));
            Assert.DoesNotContain(current, removals.Keys);
        }

        Assert.Equal(HttpStatusCode.Gone, (await PutPictureAsync(client, abandoned, SKColors.Green)).StatusCode);
        await VerifyPictureSqlAsync(current, abandoned, removed);
    }

    private async Task VerifyPictureSqlAsync(Guid current, Guid abandoned, Guid removed)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var premature = new NpgsqlCommand("""
            INSERT INTO "AccountPictureRemovals" ("PictureId", "Reason", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
            SELECT "Id", 'Removed', "CreatedAt", "CreatedAt", 0, "CreatedAt" FROM "AccountPictures" WHERE "Id" = @id
            """, connection);
        premature.Parameters.AddWithValue("id", current);
        Assert.Equal("CK_AccountPictureRemovals_Eligibility",
            (await Assert.ThrowsAsync<PostgresException>(() => premature.ExecuteNonQueryAsync())).ConstraintName);
        await using var rewrite = new NpgsqlCommand("UPDATE \"AccountPictureRemovals\" SET \"DeletedAt\" = NULL WHERE \"PictureId\" = @id", connection);
        rewrite.Parameters.AddWithValue("id", removed);
        Assert.Equal("CK_AccountPictureRemovals_Immutable",
            (await Assert.ThrowsAsync<PostgresException>(() => rewrite.ExecuteNonQueryAsync())).ConstraintName);
        await using var lateConfirm = new NpgsqlCommand("UPDATE \"AccountPictures\" SET \"AvailableAt\" = clock_timestamp() WHERE \"Id\" = @id", connection);
        lateConfirm.Parameters.AddWithValue("id", abandoned);
        Assert.Equal("CK_AccountPictures_Removal",
            (await Assert.ThrowsAsync<PostgresException>(() => lateConfirm.ExecuteNonQueryAsync())).ConstraintName);
        await using var secondCurrent = new NpgsqlCommand("""
            UPDATE "AccountPictures" SET "RemovedAt" = NULL, "AvailableAt" = "CreatedAt" WHERE "Id" = @id
            """, connection);
        secondCurrent.Parameters.AddWithValue("id", removed);
        Assert.Equal("23505", (await Assert.ThrowsAsync<PostgresException>(() => secondCurrent.ExecuteNonQueryAsync())).SqlState);
    }

    private static string PictureKey(Guid id) => $"accounts/pictures/{id:N}";

    private static async Task<HttpResponseMessage> PutPictureAsync(HttpClient client, Guid id, SKColor color)
    {
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var body = new ByteArrayContent(data.ToArray());
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        return await client.PutAsync($"/api/accounts/me/picture/{id}", body);
    }

    private static async Task<string?> ReceiptStatusAsync(HttpClient client, Guid pictureId, Guid ownerId, Guid removeTarget)
    {
        // A replayed removal of an already removed picture returns that picture's own receipt.
        using var response = await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{removeTarget}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var receipt = await client.GetAsync($"/api/accounts/me/picture/{pictureId}/receipt");
        Assert.Equal(HttpStatusCode.Gone, receipt.StatusCode);
        return (await receipt.Content.ReadFromJsonAsync<AccountPictureReceipt>())!.Status;
    }

    private async Task<DateTimeOffset> ExpiryAsync(Guid pictureId)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"ExpiresAt\" FROM \"AccountPictures\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", pictureId);
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync())!, TimeSpan.Zero);
    }

    private async Task ExpirePictureClaimsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE "AccountPictureRemovals" SET "NextAttemptAt" = clock_timestamp() - interval '1 second',
                "ClaimExpiresAt" = clock_timestamp() - interval '1 second' WHERE "DeletedAt" IS NULL
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Task RunCleanupAsync(WebApplicationFactory<Program> factory)
    {
        return factory.Services.GetServices<IHostedService>().OfType<PhotoCleanupWorker>().Single().RunBatchAsync(CancellationToken.None);
    }

    private async Task VerifyRestrictedSqlAsync(Guid activePhoto, Guid removedPhoto)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var active = new NpgsqlCommand("""
            INSERT INTO "PhotoRemovals" ("PhotoId", "Reason", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
            SELECT "Id", 'UnsubmittedExpired', "ExpiresAt", "ExpiresAt", 0, "ExpiresAt" FROM "IntakePhotos" WHERE "Id" = @id
            """, connection);
        active.Parameters.AddWithValue("id", activePhoto);
        Assert.Equal("CK_PhotoRemovals_Eligibility", (await Assert.ThrowsAsync<PostgresException>(() => active.ExecuteNonQueryAsync())).ConstraintName);
        await using var rewrite = new NpgsqlCommand("UPDATE \"PhotoRemovals\" SET \"DeletedAt\" = NULL WHERE \"PhotoId\" = @id", connection);
        rewrite.Parameters.AddWithValue("id", removedPhoto);
        Assert.Equal("CK_PhotoRemovals_Immutable", (await Assert.ThrowsAsync<PostgresException>(() => rewrite.ExecuteNonQueryAsync())).ConstraintName);
    }

    private static async Task WaitUntilAsync(DateTimeOffset deadline)
    {
        var delay = deadline - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(25);
        Assert.True(delay < TimeSpan.FromSeconds(20), "Fixture expiry must keep the scenario bounded.");

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }
    }

    private async Task ExpireCancellationRetentionAsync(Guid appointmentId)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // The owner-only disposable database lifts the immutability trigger for this single retention timestamp.
        await using var command = new NpgsqlCommand("""
            ALTER TABLE "IntakeAppointments" DISABLE TRIGGER USER;
            UPDATE "IntakeAppointments" SET "CancellationPhotosExpireAt" = clock_timestamp() - interval '1 second'
            WHERE "Id" = @id;
            ALTER TABLE "IntakeAppointments" ENABLE TRIGGER USER;
            """, connection, transaction);
        command.Parameters.AddWithValue("id", appointmentId);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private async Task ExpireClaimsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE "PhotoRemovals" SET "NextAttemptAt" = clock_timestamp() - interval '1 second',
                "ClaimExpiresAt" = clock_timestamp() - interval '1 second' WHERE "DeletedAt" IS NULL
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task VerifyPrematureSqlAsync(Guid unexpiredPhoto, Guid cancelledPhoto)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        var attempts = new[]
        {
            (Id: unexpiredPhoto, Reason: "UnsubmittedExpired", Sql: "SELECT \"ExpiresAt\" FROM \"IntakePhotos\" WHERE \"Id\" = @id"),
            (Id: cancelledPhoto, Reason: "CancelledRetentionExpired", Sql: "SELECT c.\"CancelledAt\" FROM \"IntakeAppointments\" c JOIN \"AppointmentPhotos\" a ON a.\"AppointmentId\" = c.\"Id\" WHERE a.\"PhotoId\" = @id")
        };

        foreach (var (id, reason, sql) in attempts)
        {
            await using var query = new NpgsqlCommand(sql, connection);
            query.Parameters.AddWithValue("id", id);
            var timestamp = (DateTime)(await query.ExecuteScalarAsync())!;
            await using var insert = new NpgsqlCommand("""
                INSERT INTO "PhotoRemovals" ("PhotoId", "Reason", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
                VALUES (@id, @reason, @at, @at, 0, @at)
                """, connection);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("reason", reason);
            insert.Parameters.AddWithValue("at", timestamp);
            Assert.Equal("CK_PhotoRemovals_Eligibility", (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).ConstraintName);
        }
    }
}
