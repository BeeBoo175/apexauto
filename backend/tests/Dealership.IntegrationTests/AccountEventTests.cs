using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AccountEventTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AccountHistoryIsAppendOnlyReferencedAndShapedByTheDatabase()
    {
        using var factory = fixture.CreateFactory();
        var (administrator, employee) = await ProvisionAsync(factory);
        var recorded = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), employee, administrator,
                AccountEventKind.Suspended, "  Under review  ", [new AccountChange("Status", "Active", "Suspended")],
                recorded));
            database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), employee, employee,
                AccountEventKind.UsernameChanged, null, [new AccountChange("Username", "old.name", "new.name")],
                recorded.AddSeconds(1)));
            await database.SaveChangesAsync();
        }

        var eventId = await ScalarAsync<Guid>(
            "SELECT \"Id\" FROM \"AccountEvents\" WHERE \"Kind\" = 'Suspended'");
        Assert.Equal("Under review", await ScalarAsync<string>(
            "SELECT \"Reason\" FROM \"AccountEvents\" WHERE \"Kind\" = 'Suspended'"));

        await AssertRejectedAsync(
            "UPDATE \"AccountEvents\" SET \"Reason\" = 'Rewritten' WHERE \"Id\" = @id", eventId,
            PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync("DELETE FROM \"AccountEvents\" WHERE \"Id\" = @id", eventId,
            PostgresErrorCodes.InsufficientPrivilege);
        await AssertRejectedAsync("TRUNCATE \"AccountEvents\"", eventId, PostgresErrorCodes.InsufficientPrivilege);
        await AssertRejectedAsync(Insert("Suspended", "NULL", employee), eventId, PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(Insert("UsernameChanged", "'Not allowed'", employee), eventId,
            PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(Insert("Promoted", "NULL", employee), eventId, PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(Insert("PictureRemoved", "NULL", Guid.NewGuid()), eventId,
            PostgresErrorCodes.ForeignKeyViolation);
    }

    private static string Insert(string kind, string reason, Guid target)
    {
        return $"INSERT INTO \"AccountEvents\" (\"Id\", \"TargetId\", \"ActorId\", \"Kind\", \"Reason\", \"RecordedAt\") " +
            $"SELECT gen_random_uuid(), '{target}', \"TargetId\", '{kind}', {reason}, now() FROM \"AccountEvents\" LIMIT 1";
    }

    private async Task AssertRejectedAsync(string sql, Guid id, string sqlState)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, failure.SqlState);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<(Guid Administrator, Guid Employee)> ProvisionAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var staff = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
        var administrator = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<Dealership.Application.Branches.BranchProvisioningService>()
            .ProvisionBranchAsync(new(branch, "ApexAuto Glenmarie", "12 Jalan Pengaturcara", "Shah Alam", "Selangor",
                "+60355651234", "GLN"), CancellationToken.None);
        Assert.True((await staff.ProvisionStaffAsync(new(administrator, $"admin.{Guid.NewGuid():N}@example.invalid",
            "Password123!", AccountRoles.Administrator, null, "Amir Hakim", "0123456789"), CancellationToken.None)).Created);
        Assert.True((await staff.ProvisionStaffAsync(new(employee, $"agent.{Guid.NewGuid():N}@example.invalid",
            "Password123!", AccountRoles.SalesAgent, branch, "Aina Sofia", "0129876543"), CancellationToken.None)).Created);
        return (administrator, employee);
    }
}
