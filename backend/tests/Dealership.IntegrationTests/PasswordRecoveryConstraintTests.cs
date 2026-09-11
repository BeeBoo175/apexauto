using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PasswordRecoveryConstraintTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RestrictedSqlProtectsOwnershipUniquenessLifetimeAndImmutableCompletedReceipt()
    {
        using var host = new PasswordRecoveryHost(database);
        var email = $"farah.{Guid.NewGuid():N}@example.invalid";
        using (var scope = host.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            Assert.True((await users.CreateAsync(new AccountUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                AccountUsername = TestUsernames.Next(),
                Role = AccountRoles.GeneralManager
            }, "Password123!")).Succeeded);
        }

        await host.RequestAsync(email);
        var challenge = await host.LatestAsync(email);
        await using var runtime = new NpgsqlConnection(database.RuntimeConnectionString);
        await runtime.OpenAsync();
        await AssertInsertRejectedAsync(runtime, challenge, Guid.NewGuid(), challenge.ExpiresAt, PostgresErrorCodes.ForeignKeyViolation);
        await AssertInsertRejectedAsync(runtime, challenge, challenge.UserId, challenge.ExpiresAt, PostgresErrorCodes.UniqueViolation);
        await AssertInsertRejectedAsync(runtime, challenge, challenge.UserId, challenge.CreatedAt, PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(runtime, challenge, "UPDATE \"PasswordResetChallenges\" SET \"ConsumedAt\" = \"CreatedAt\" WHERE \"Id\" = @id",
            PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(runtime, challenge, "DELETE FROM \"PasswordResetChallenges\" WHERE \"Id\" = @id",
            PostgresErrorCodes.InsufficientPrivilege);
        await AssertRejectedAsync(runtime, challenge, "ALTER TABLE \"PasswordResetChallenges\" DISABLE TRIGGER protect_password_reset",
            PostgresErrorCodes.InsufficientPrivilege);
        await using (var maintenance = new NpgsqlConnection(database.ConnectionString))
        {
            await maintenance.OpenAsync();
            await AssertRejectedAsync(maintenance, challenge, "DELETE FROM \"AspNetUsers\" WHERE \"Id\" = @user",
                PostgresErrorCodes.ForeignKeyViolation);
        }

        Assert.Equal(PasswordResetStatus.Succeeded, (await host.ResetAsync(host.Token(challenge))).Status);
        foreach (var assignment in new[]
        {
            "\"CompletedSecurityStamp\" = 'changed'", "\"ConsumedAt\" = NULL, \"CompletedSecurityStamp\" = NULL",
            "\"ExpiresAt\" = \"ExpiresAt\" + interval '1 minute'", "\"UserId\" = gen_random_uuid()",
            "\"RecipientEmail\" = 'siti@example.invalid'"
        })
        {
            await AssertRejectedAsync(runtime, challenge, $"UPDATE \"PasswordResetChallenges\" SET {assignment} WHERE \"Id\" = @id",
                PostgresErrorCodes.CheckViolation);
        }
    }

    private static async Task AssertInsertRejectedAsync(NpgsqlConnection connection, PasswordResetChallenge challenge,
        Guid userId, DateTimeOffset expires, string sqlState)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "PasswordResetChallenges"
                ("Id", "UserId", "RecipientEmail", "CreatedAt", "ExpiresAt", "DeliveryAttempts", "NextDeliveryAt")
            VALUES (@id, @user, @email, @created, @expires, 0, @created)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("email", challenge.RecipientEmail!);
        command.Parameters.AddWithValue("created", challenge.CreatedAt);
        command.Parameters.AddWithValue("expires", expires);
        var failure = await Assert.ThrowsAsync<PostgresException>(async () => await command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, failure.SqlState);
    }

    private static async Task AssertRejectedAsync(NpgsqlConnection connection, PasswordResetChallenge challenge,
        string sql, string sqlState)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", challenge.Id);
        command.Parameters.AddWithValue("user", challenge.UserId);
        var failure = await Assert.ThrowsAsync<PostgresException>(async () => await command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, failure.SqlState);
    }
}
