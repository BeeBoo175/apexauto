using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AccountErasureSchemaTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string CloseUser = """
        UPDATE "AspNetUsers" SET "ClosedAt" = now(), "IsActive" = false, "SuspendedAt" = NULL, "PasswordHash" = NULL,
            "EmailConfirmed" = false, "Email" = NULL, "NormalizedEmail" = NULL, "UserName" = NULL,
            "NormalizedUserName" = NULL, "AccountUsername" = NULL
        WHERE "Id" = {0}
        """;

    [Fact]
    public async Task AccountHistoryCanOnlyLoseItsReasonAndChangesAfterTheTargetClosed()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var id = await UserIdAsync(database, registration.Email!);
        var eventId = Guid.NewGuid();
        database.AccountEvents.Add(new AccountEvent(eventId, id, id, AccountEventKind.UsernameChanged, null,
            [new("Username", "old.name", "new.name")], DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        const string minimize = "UPDATE \"AccountEvents\" SET \"Changes\" = NULL, \"MinimizedAt\" = now() WHERE \"Id\" = {0}";

        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation, minimize, eventId);

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(CloseUser, id));
        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation,
            "UPDATE \"AccountEvents\" SET \"ActorId\" = {0}, \"Changes\" = NULL, \"MinimizedAt\" = now() WHERE \"Id\" = {1}",
            Guid.NewGuid(), eventId);
        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation,
            "UPDATE \"AccountEvents\" SET \"MinimizedAt\" = now() WHERE \"Id\" = {0}", eventId);
        await ExpectFailureAsync(database, PostgresErrorCodes.InsufficientPrivilege,
            "DELETE FROM \"AccountEvents\" WHERE \"Id\" = {0}", eventId);

        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(minimize, eventId));
        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation, minimize, eventId);
        var stored = await database.AccountEvents.AsNoTracking().SingleAsync(item => item.Id == eventId);
        Assert.NotNull(stored.MinimizedAt);
        Assert.Null(stored.Changes);
    }

    [Fact]
    public async Task OneAccountHasOneErasedReceiptAndAMinimizedRowCannotKeepAReason()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var id = await UserIdAsync(database, registration.Email!);
        // A receipt for an account that is still open is refused, so no one can pre-empt a real erasure with a forged one.
        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation,
            "INSERT INTO \"AccountEvents\" (\"Id\", \"TargetId\", \"ActorId\", \"Kind\", \"RecordedAt\", \"MinimizedAt\") " +
            "VALUES ({0}, {1}, {1}, 'Erased', now(), now())", Guid.NewGuid(), id);
        await database.Database.ExecuteSqlRawAsync(
            "UPDATE \"AspNetUsers\" SET \"ClosedAt\" = now(), \"IsActive\" = false, \"PasswordHash\" = NULL, " +
            "\"EmailConfirmed\" = false, \"Email\" = NULL, \"NormalizedEmail\" = NULL, \"UserName\" = NULL, " +
            "\"NormalizedUserName\" = NULL, \"AccountUsername\" = NULL WHERE \"Id\" = {0}", id);
        database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), id, id, AccountEventKind.Erased, null, null,
            DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), id, id, AccountEventKind.Erased, null, null,
            DateTimeOffset.UtcNow));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        database.ChangeTracker.Clear();

        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation,
            "INSERT INTO \"AccountEvents\" (\"Id\", \"TargetId\", \"ActorId\", \"Kind\", \"Reason\", \"RecordedAt\", \"MinimizedAt\") " +
            "VALUES ({0}, {1}, {1}, 'UsernameChanged', 'kept', now(), now())", Guid.NewGuid(), id);
    }

    [Fact]
    public async Task ResetChallengeRecipientCanOnlyBeRemovedFromAClosedAccount()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var id = await UserIdAsync(database, registration.Email!);
        var challengeId = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PasswordResetChallenges"
                ("Id", "UserId", "RecipientEmail", "CreatedAt", "ExpiresAt", "DeliveryAttempts", "NextDeliveryAt")
            VALUES ({challengeId}, {id}, {registration.Email}, now(), now() + interval '1 hour', 0, now())
            """);
        const string remove = "UPDATE \"PasswordResetChallenges\" SET \"RecipientEmail\" = NULL WHERE \"Id\" = {0}";

        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation, remove, challengeId);
        await ExpectFailureAsync(database, PostgresErrorCodes.CheckViolation,
            "UPDATE \"PasswordResetChallenges\" SET \"RecipientEmail\" = 'other@example.invalid' WHERE \"Id\" = {0}",
            challengeId);

        await database.Database.ExecuteSqlRawAsync(CloseUser, id);
        Assert.Equal(1, await database.Database.ExecuteSqlRawAsync(remove, challengeId));
        Assert.Null(await database.PasswordResetChallenges.AsNoTracking().Where(item => item.Id == challengeId)
            .Select(item => item.RecipientEmail).SingleAsync());
    }

    private static Task<Guid> UserIdAsync(DealershipDbContext database, string email)
    {
        var normalized = email.ToUpperInvariant();
        return database.Users.Where(user => user.NormalizedEmail == normalized).Select(user => user.Id).SingleAsync();
    }

    private static async Task ExpectFailureAsync(DealershipDbContext database, string sqlState, string sql,
        params object[] arguments)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(sql, arguments));
        var postgres = Assert.IsType<PostgresException>(failure is DbUpdateException ? failure.InnerException : failure);
        Assert.Equal(sqlState, postgres.SqlState);
        await transaction.RollbackAsync();
    }
}
