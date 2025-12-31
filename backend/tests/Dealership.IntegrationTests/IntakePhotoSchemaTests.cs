using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class IntakePhotoSchemaTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RestrictedSqlProtectsOwnerRelationshipContentLifetimeAndConfirmedReceipt()
    {
        using var services = fixture.CreateServices();
        using var scope = services.CreateScope();
        var registration = DevelopmentFixtureManifest.Customer;
        Assert.Equal(RegistrationStatus.Succeeded, (await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None)).Status);
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await database.EmailVerificationChallenges.SingleAsync();
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(VerificationStatus.Accepted, (await scope.ServiceProvider.GetRequiredService<IEmailVerificationStore>()
            .ConfirmAsync(token, CancellationToken.None)).Status);
        var store = scope.ServiceProvider.GetRequiredService<IIntakePhotoStore>();
        var id = Guid.NewGuid();
        var content = new PhotoContent("image/jpeg", 100, new string('a', 64), 8, 8);
        Assert.Equal(PhotoStatus.Pending, (await store.PrepareAsync(challenge.UserId, id, PhotoAngle.Driver, content,
            TimeSpan.FromHours(24), CancellationToken.None)).Status);
        Assert.Equal(PhotoStatus.Available, (await store.ConfirmAsync(challenge.UserId, id, CancellationToken.None)).Status);

        await RejectedAsync("INSERT INTO \"IntakePhotos\" SELECT * FROM \"IntakePhotos\" WHERE \"Id\" = @id",
            id, PostgresErrorCodes.UniqueViolation);
        await RejectedAsync("""
            INSERT INTO "IntakePhotos" ("Id", "OwnerId", "Angle", "ObjectKey", "ContentType", "Bytes", "Sha256",
                "Width", "Height", "CreatedAt", "ExpiresAt", "AvailableAt")
            SELECT @other, "OwnerId", "Angle", "ObjectKey", "ContentType", "Bytes", "Sha256",
                "Width", "Height", "CreatedAt", "ExpiresAt", NULL FROM "IntakePhotos" WHERE "Id" = @id
            """, id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"IntakePhotos\" SET \"OwnerId\" = @other WHERE \"Id\" = @id",
            id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"IntakePhotos\" SET \"Angle\" = 'Rear' WHERE \"Id\" = @id",
            id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"IntakePhotos\" SET \"Sha256\" = repeat('b', 64) WHERE \"Id\" = @id",
            id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"IntakePhotos\" SET \"ExpiresAt\" = \"ExpiresAt\" + interval '1 hour' WHERE \"Id\" = @id",
            id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("UPDATE \"IntakePhotos\" SET \"AvailableAt\" = NULL WHERE \"Id\" = @id",
            id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync("DELETE FROM \"IntakePhotos\" WHERE \"Id\" = @id", id, PostgresErrorCodes.InsufficientPrivilege);
        await RejectedAsync("DELETE FROM \"CustomerProfiles\" WHERE \"UserId\" = (SELECT \"OwnerId\" FROM \"IntakePhotos\" WHERE \"Id\" = @id)",
            id, PostgresErrorCodes.ForeignKeyViolation, maintenance: true);

        const string copy = """
            INSERT INTO "IntakePhotos" ("Id", "OwnerId", "Angle", "ObjectKey", "ContentType", "Bytes", "Sha256",
                "Width", "Height", "CreatedAt", "ExpiresAt", "AvailableAt")
            SELECT @other, {0}, {1}, 'intake/photos/' || replace(@other::text, '-', ''), "ContentType", {2}, "Sha256",
                "Width", "Height", "CreatedAt", {3}, NULL FROM "IntakePhotos" WHERE "Id" = @id
            """;
        await RejectedAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, copy,
            "@other", "\"Angle\"", "\"Bytes\"", "\"ExpiresAt\""), id, PostgresErrorCodes.ForeignKeyViolation);
        await RejectedAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, copy,
            "\"OwnerId\"", "'Roof'", "\"Bytes\"", "\"ExpiresAt\""), id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, copy,
            "\"OwnerId\"", "\"Angle\"", "10485761", "\"ExpiresAt\""), id, PostgresErrorCodes.CheckViolation);
        await RejectedAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, copy,
            "\"OwnerId\"", "\"Angle\"", "\"Bytes\"", "\"CreatedAt\""), id, PostgresErrorCodes.CheckViolation);
    }

    private async Task RejectedAsync(string sql, Guid id, string state, bool maintenance = false)
    {
        await using var connection = new NpgsqlConnection(maintenance ? fixture.ConnectionString : fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("other", Guid.NewGuid());
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, exception.SqlState);
    }
}
