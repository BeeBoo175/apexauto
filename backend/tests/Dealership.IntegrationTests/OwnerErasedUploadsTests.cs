using System.Net;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class OwnerErasedUploadsTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string CloseUser = """
        UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "SuspendedAt" = NULL, "PasswordHash" = NULL,
            "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "UserName" = NULL,
            "NormalizedUserName" = NULL, "AccountUsername" = NULL
        WHERE "Id" = {0}
        """;

    private const string RemoveUpload = """
        INSERT INTO "PhotoRemovals" ("PhotoId", "Reason", "RequestedAt", "EligibleAt", "Attempts", "NextAttemptAt")
        SELECT {0}, 'OwnerErased', u."ClosedAt", u."ClosedAt", 0, u."ClosedAt" FROM "AspNetUsers" u WHERE u."Id" = {1}
        """;

    [Fact]
    public async Task AnUploadIsRemovableOnlyAfterItsOwnerIsClosed()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var photoId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await IntakePhotoFixture.UploadAsync(client, photoId, IntakePhotoFixture.Image())).StatusCode);
        var ownerId = await BookingTestData.OwnerAsync(factory, registration);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();

        // The receipt trigger reads the owner's closed state, so an open owner has no row to read the time from.
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"PhotoRemovals\" (\"PhotoId\", \"Reason\", \"RequestedAt\", \"EligibleAt\", \"Attempts\", " +
                "\"NextAttemptAt\") VALUES ({0}, 'OwnerErased', now(), now(), 0, now())", photoId));
            var postgres = Assert.IsType<PostgresException>(failure is DbUpdateException ? failure.InnerException : failure);
            Assert.Equal("CK_PhotoRemovals_Eligibility", postgres.ConstraintName);
            await transaction.RollbackAsync();
        }

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(CloseUser, ownerId));
        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(RemoveUpload, photoId, ownerId));
    }

    [Fact]
    public async Task APictureHashIsKeptWhileItsOwnerIsOpen()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        var ownerId = await BookingTestData.OwnerAsync(factory, registration);
        var pictureId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await database.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AccountPictures" ("Id", "UserId", "ObjectKey", "ContentType", "Bytes", "Sha256", "CreatedAt",
                "ExpiresAt", "AvailableAt")
            VALUES ({0}, {1}, {2}, 'image/png', 10, repeat('a', 64), now(), now() + interval '1 day', now())
            """, pictureId, ownerId, $"accounts/pictures/{pictureId:N}");

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(
            "UPDATE \"AccountPictures\" SET \"Sha256\" = NULL WHERE \"Id\" = {0}", pictureId));
        var postgres = Assert.IsType<PostgresException>(failure is DbUpdateException ? failure.InnerException : failure);
        Assert.Equal("CK_AccountPictures_HashRetained", postgres.ConstraintName);
    }
}
