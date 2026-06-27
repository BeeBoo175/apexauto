using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PurchaseSettingsSchemaTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RestrictedSqlEnforcesPromoIdentitySingletonRangesAndUpdaterRetention()
    {
        var actor = Guid.NewGuid();
        var email = $"hafiz.{actor:N}@example.invalid";
        await using var context = NewContext();
        context.Users.Add(new AccountUser
        {
            Id = actor,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            AccountUsername = TestUsernames.Next(),
            NormalizedUserName = email.ToUpperInvariant()
        });
        context.PurchaseSettings.Add(new(500m, 0.08m, actor, DateTimeOffset.UtcNow));
        context.PromoCodes.Add(new(Guid.NewGuid(), "APEX500", 500m, DateTimeOffset.UtcNow.AddDays(1), true));
        context.PromoCodes.Add(new(Guid.NewGuid(), "apex500", 100m, DateTimeOffset.UtcNow.AddDays(1), true));
        await context.SaveChangesAsync();

        await RejectedAsync("INSERT INTO \"PromoCodes\" SELECT gen_random_uuid(), 'APEX500', 1, now(), true",
            PostgresErrorCodes.UniqueViolation);
        foreach (var code in new[] { " APEX500", "APEX500\t", "\u00a0APEX500", "\u3000", "" })
        {
            await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "INSERT INTO \"PromoCodes\" (\"Id\", \"Code\", \"Discount\", \"ExpiresAtUtc\", \"IsActive\") " +
                "VALUES (gen_random_uuid(), @code, 1, now(), true)", connection);
            command.Parameters.AddWithValue("code", code);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }

        foreach (var assignment in new[] { "\"Discount\" = -1", "\"Discount\" = 0", "\"Discount\" = 100000.01", "\"Version\" = 0, \"Discount\" = 1", "\"Discount\" = 'NaN'", "\"ExpiresAtUtc\" = 'infinity'" })
        {
            await RejectedAsync($"UPDATE \"PromoCodes\" SET {assignment}", PostgresErrorCodes.CheckViolation);
        }

        foreach (var assignment in new[] { "\"Id\" = 2", "\"ProcessingFee\" = -1", "\"ProcessingFee\" = 'NaN'",
            "\"ProcessingFee\" = 5000.01", "\"SstRate\" = -0.01", "\"SstRate\" = 0.21", "\"SstRate\" = 1.01",
            "\"MaxAgentDiscountAmount\" = -1", "\"MaxAgentDiscountAmount\" = 5000.01", "\"MaxAgentDiscountAmount\" = 'NaN'",
            "\"ReservationHoldHours\" = 0", "\"ReservationHoldHours\" = 169", "\"Version\" = 0", "\"UpdatedAtUtc\" = 'infinity'" })
        {
            await RejectedAsync($"UPDATE \"PurchaseSettings\" SET {assignment}", PostgresErrorCodes.CheckViolation);
        }

        await RejectedAsync("INSERT INTO \"PurchaseSettings\" SELECT * FROM \"PurchaseSettings\"", PostgresErrorCodes.UniqueViolation);
        await RejectedAsync("UPDATE \"PurchaseSettings\" SET \"UpdatedById\" = gen_random_uuid()", PostgresErrorCodes.ForeignKeyViolation);
        await RejectedAsync($"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = '{actor}'", PostgresErrorCodes.ForeignKeyViolation, maintenance: true);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"PurchaseSettings\" SET \"SstRate\" = 0.12345678901234567890123456789");
        Assert.Equal(0.1234567890123456789012345679m,
            await context.PurchaseSettings.AsNoTracking().Select(item => item.SstRate).SingleAsync());

        await using var competitor = NewContext();
        var stale = await competitor.PurchaseSettings.SingleAsync();
        await context.PurchaseSettings.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ProcessingFee, 600m).SetProperty(item => item.Version, 2));
        competitor.Entry(stale).Property(item => item.ProcessingFee).CurrentValue = 700m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => competitor.SaveChangesAsync());
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(database.RuntimeConnectionString).Options);

    private async Task RejectedAsync(string sql, string state, bool maintenance = false)
    {
        await using var connection = new NpgsqlConnection(maintenance ? database.ConnectionString : database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }
}
