using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SkiaSharp;

namespace Dealership.IntegrationTests;

// Uses its own fixture because CheckInTestData provisions a uniquely named branch per database.
public sealed class ListingPhotoCleanupTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AbandonedUploadsAreCleanedWhileConfirmedLibraryAndReferencedPhotosSurvive()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        var vehicleId = (await data.SaveBundleAsync(appointment.Id)).VehicleId;
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var (manager, _) = await ListingPhotoTests.ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var bytes = IntakePhotoFixture.Image();
        var referenced = Guid.NewGuid();
        var library = Guid.NewGuid();
        var abandoned = Guid.NewGuid();
        Assert.Equal(ListingPhotoStatus.Available, (await ListingPhotoTests.UploadAsync(data.Factory, manager, vehicleId, referenced, bytes)).Status);
        Assert.Equal(ListingPhotoStatus.Available,
            (await ListingPhotoTests.UploadAsync(data.Factory, manager, vehicleId, library, IntakePhotoFixture.Image(color: SKColors.Red))).Status);
        var saved = await services.GetRequiredService<ListingService>().SaveAsync(manager, vehicleId,
            new ListingDraftRequest(Guid.NewGuid(), 0, 60000m, 90m, VehicleTransmission.Automatic, VehicleFuel.Petrol,
                "Referenced photography")
            { PhotoIds = [referenced] }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, saved.Status);

        // The upload reached storage but was never confirmed, as after a lost acknowledgement the client abandoned.
        using (var outage = fixture.CreateFactory(failAfterPut: true))
        {
            await Assert.ThrowsAsync<PhotoStorageException>(() => ListingPhotoTests.UploadAsync(outage, manager, vehicleId, abandoned,
                IntakePhotoFixture.Image(color: SKColors.Green)));
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(ListingKey(abandoned)));
        var database = services.GetRequiredService<DealershipDbContext>();
        var lifetime = TimeSpan.FromHours(1).TotalHours;
        using var cleanup = fixture.CreateFactory(pendingUploadLifetimeHours: lifetime);
        await RunCleanupAsync(cleanup);
        Assert.True(await fixture.OwnedObjectExistsAsync(ListingKey(abandoned)));
        Assert.Empty(await database.ListingPhotoRemovals.AsNoTracking().ToArrayAsync());

        // Age the unconfirmed upload past its lifetime through the owner role instead of waiting for the clock.
        await using (var owner = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await owner.OpenAsync();
            await RetentionSql.AgeTableAsync(owner, "ListingPhotos",
                $"\"CreatedAt\" = \"CreatedAt\" - interval '2 hours' WHERE \"Id\" = '{abandoned}'");
        }

        using (var failing = fixture.CreateFactory(failBeforeDelete: true, pendingUploadLifetimeHours: lifetime))
        {
            await RunCleanupAsync(failing);
        }

        Assert.True(await fixture.OwnedObjectExistsAsync(ListingKey(abandoned)));
        await ExpireListingClaimsAsync();
        using (var lostAcknowledgement = fixture.CreateFactory(failAfterDelete: true, pendingUploadLifetimeHours: lifetime))
        {
            await RunCleanupAsync(lostAcknowledgement);
        }

        Assert.False(await fixture.OwnedObjectExistsAsync(ListingKey(abandoned)));
        await ExpireListingClaimsAsync();
        await RunCleanupAsync(cleanup);
        var removals = await database.ListingPhotoRemovals.AsNoTracking().ToArrayAsync();
        Assert.Equal(abandoned, Assert.Single(removals).PhotoId);
        Assert.NotNull(removals[0].DeletedAt);
        Assert.True(await fixture.OwnedObjectExistsAsync(ListingKey(referenced)));
        Assert.True(await fixture.OwnedObjectExistsAsync(ListingKey(library)));
        Assert.Equal(ListingPhotoStatus.Available, (await services.GetRequiredService<ListingPhotoService>()
            .ReceiptAsync(manager, vehicleId, library, CancellationToken.None)).Status);
        Assert.Equal(ListingPhotoStatus.Removed, (await services.GetRequiredService<ListingPhotoService>()
            .ReceiptAsync(manager, vehicleId, abandoned, CancellationToken.None)).Status);
        Assert.Equal(ListingPhotoStatus.Removed,
            (await ListingPhotoTests.UploadAsync(data.Factory, manager, vehicleId, abandoned, IntakePhotoFixture.Image(color: SKColors.Green))).Status);
        await VerifyCleanupSqlAsync(library, referenced, abandoned);
    }

    private async Task VerifyCleanupSqlAsync(Guid library, Guid referenced, Guid abandoned)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();

        foreach (var photo in new[] { library, referenced })
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO "ListingPhotoRemovals" ("PhotoId", "EligibleAt", "RequestedAt", "Attempts", "NextAttemptAt")
                SELECT "Id", "CreatedAt", "CreatedAt", 0, "CreatedAt" FROM "ListingPhotos" WHERE "Id" = @id
                """, connection);
            insert.Parameters.AddWithValue("id", photo);
            Assert.Equal("CK_ListingPhotoRemovals_Eligibility",
                (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).ConstraintName);
        }

        await using var rewrite = new NpgsqlCommand("UPDATE \"ListingPhotoRemovals\" SET \"DeletedAt\" = NULL WHERE \"PhotoId\" = @id", connection);
        rewrite.Parameters.AddWithValue("id", abandoned);
        Assert.Equal("CK_ListingPhotoRemovals_Immutable",
            (await Assert.ThrowsAsync<PostgresException>(() => rewrite.ExecuteNonQueryAsync())).ConstraintName);
        await using var lateConfirm = new NpgsqlCommand("UPDATE \"ListingPhotos\" SET \"AvailableAt\" = clock_timestamp() WHERE \"Id\" = @id", connection);
        lateConfirm.Parameters.AddWithValue("id", abandoned);
        Assert.Equal("CK_ListingPhotos_Removal",
            (await Assert.ThrowsAsync<PostgresException>(() => lateConfirm.ExecuteNonQueryAsync())).ConstraintName);
    }

    private async Task ExpireListingClaimsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE "ListingPhotoRemovals" SET "NextAttemptAt" = clock_timestamp() - interval '1 second',
                "ClaimExpiresAt" = clock_timestamp() - interval '1 second' WHERE "DeletedAt" IS NULL
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string ListingKey(Guid id) => $"listings/photos/{id:N}";

    private static Task RunCleanupAsync(WebApplicationFactory<Program> factory)
    {
        return factory.Services.GetServices<IHostedService>().OfType<PhotoCleanupWorker>().Single()
            .RunBatchAsync(CancellationToken.None);
    }
}
