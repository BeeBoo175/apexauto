using System.Net;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PhotoCleanupSnapshotTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task StaleRemovalAndAttachmentSnapshotsCannotCrossCommittedEvidenceBoundaries()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var owner = await BookingTestData.OwnerAsync(factory, registration);
        var request = await BookingTestData.RequestAsync(factory, client);
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using (var stale = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead))
        {
            await SetSnapshotAsync(connection, stale);
            var receipt = await BookingTestData.BookAsync(client, request);
            await using var removal = new NpgsqlCommand("""
                INSERT INTO "PhotoRemovals" ("PhotoId", "Reason", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
                SELECT "Id", 'UnsubmittedExpired', "ExpiresAt", "ExpiresAt", 0, "ExpiresAt"
                FROM "IntakePhotos" WHERE "Id" = @id
                """, connection, stale);
            removal.Parameters.AddWithValue("id", receipt.Photos[0].Id);
            Assert.Equal(PostgresErrorCodes.SerializationFailure,
                (await Assert.ThrowsAsync<PostgresException>(() => removal.ExecuteNonQueryAsync())).SqlState);
        }

        var photoId = Guid.NewGuid();
        using var shortLifetime = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(2).TotalHours);
        using var shortClient = shortLifetime.CreateClient();
        shortClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(shortClient, photoId, IntakePhotoFixture.Image())).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var expires = await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().IntakePhotos
                .Where(photo => photo.Id == photoId).Select(photo => photo.ExpiresAt).SingleAsync();
            var delay = expires - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(25);
            Assert.True(delay < TimeSpan.FromSeconds(3));

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }
        }

        await using (var stale = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead))
        {
            await SetSnapshotAsync(connection, stale);
            using var scope = factory.Services.CreateScope();
            await using var transfer = await scope.ServiceProvider.GetRequiredService<IPhotoTransferLock>()
                .TryAcquireAsync(photoId, CancellationToken.None);
            Assert.NotNull(transfer);
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<PhotoCleanupStore>().ClaimAsync(photoId, CancellationToken.None));
            await using var attach = new NpgsqlCommand("""
                INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
                VALUES (@appointment, @owner, @photo, 'Driver')
                """, connection, stale);
            attach.Parameters.AddWithValue("appointment", request.Id);
            attach.Parameters.AddWithValue("owner", owner);
            attach.Parameters.AddWithValue("photo", photoId);
            Assert.Equal(PostgresErrorCodes.SerializationFailure,
                (await Assert.ThrowsAsync<PostgresException>(() => attach.ExecuteNonQueryAsync())).SqlState);
        }

        await using var rejected = new NpgsqlCommand("""
            INSERT INTO "AppointmentPhotos" ("AppointmentId", "OwnerId", "PhotoId", "Angle")
            VALUES (@appointment, @owner, @photo, 'Driver')
            """, connection);
        rejected.Parameters.AddWithValue("appointment", request.Id);
        rejected.Parameters.AddWithValue("owner", owner);
        rejected.Parameters.AddWithValue("photo", photoId);
        Assert.Equal("CK_AppointmentPhotos_Available",
            (await Assert.ThrowsAsync<PostgresException>(() => rejected.ExecuteNonQueryAsync())).ConstraintName);
    }

    private static async Task SetSnapshotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM \"IntakePhotos\"", connection, transaction);
        await command.ExecuteScalarAsync();
    }
}
