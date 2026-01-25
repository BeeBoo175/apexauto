using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PhotoCleanupTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CleanupCoordinatesTransfersRetainsEvidenceAndReconcilesLostDeletionAcknowledgementsAfterRestart()
    {
        using var factory = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(10).TotalHours,
            cancelledRetentionDays: TimeSpan.FromSeconds(15).TotalDays);
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
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/intake/appointments/{cancelled.Id}/cancellation", null)).StatusCode);
        await RunCleanupAsync(factory);
        Assert.True(await fixture.OwnedObjectExistsAsync(cancelled.Photos[0].Id));
        var unexpired = Guid.NewGuid();
        using (var longLifetime = fixture.CreateFactory(unsubmittedLifetimeHours: 24))
        using (var longClient = longLifetime.CreateClient())
        {
            longClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(longClient, unexpired, IntakePhotoFixture.Image())).StatusCode);
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
            var expires = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().AppointmentCancellations
                .Where(item => item.AppointmentId == cancelled.Id).Select(item => item.PhotosExpireAt).SingleAsync();
            await WaitUntilAsync(expires);
        }

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
            (Id: cancelledPhoto, Reason: "CancelledRetentionExpired", Sql: "SELECT c.\"CancelledAt\" FROM \"AppointmentCancellations\" c JOIN \"AppointmentPhotos\" a ON a.\"AppointmentId\" = c.\"AppointmentId\" WHERE a.\"PhotoId\" = @id")
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
