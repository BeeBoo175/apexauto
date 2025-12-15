using Dealership.Infrastructure.Identity;
using Dealership.Domain.Branches;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class StaffSchemaTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RestrictedSqlEnforcesRoleBranchAssignmentAndParentRetention()
    {
        var branch = new Branch(Guid.NewGuid(), "ApexAuto Glenmarie", "12 Jalan Pengaturcara U1/51A",
            "Shah Alam", "Selangor", "+60355651234", DateTimeOffset.UtcNow);
        var email = $"aina.sofia.{Guid.NewGuid():N}@example.invalid";
        var user = new AccountUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
        await using (var context = NewContext())
        {
            context.Branches.Add(branch);
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var userId = user.Id;

        await AssertRejectedAsync("UPDATE \"AspNetUsers\" SET \"Role\" = 'SalesAgent' WHERE \"Id\" = @user", userId,
            branch.Id, PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync("UPDATE \"AspNetUsers\" SET \"BranchId\" = @branch WHERE \"Id\" = @user", userId,
            branch.Id, PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync("UPDATE \"AspNetUsers\" SET \"Role\" = 'SalesAgent', \"BranchId\" = @branch WHERE \"Id\" = @user",
            userId, Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation);
        await AssertRejectedAsync("UPDATE \"AspNetUsers\" SET \"Role\" = 'Owner', \"BranchId\" = @branch WHERE \"Id\" = @user",
            userId, branch.Id, PostgresErrorCodes.CheckViolation);

        await ExecuteAsync("UPDATE \"AspNetUsers\" SET \"Role\" = 'SalesAgent', \"BranchId\" = @branch WHERE \"Id\" = @user",
            userId, branch.Id);
        await AssertRejectedAsync("DELETE FROM \"Branches\" WHERE \"Id\" = @branch", userId, branch.Id,
            PostgresErrorCodes.ForeignKeyViolation, maintenance: true);
        await ExecuteAsync("UPDATE \"AspNetUsers\" SET \"Role\" = 'GeneralManager', \"BranchId\" = NULL WHERE \"Id\" = @user",
            userId, branch.Id);
    }

    private DealershipDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(database.RuntimeConnectionString).Options;
        return new DealershipDbContext(options);
    }

    private async Task AssertRejectedAsync(string sql, Guid userId, Guid branchId, string state,
        bool maintenance = false)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await ExecuteAsync(sql, userId, branchId, maintenance));
        Assert.Equal(state, exception.SqlState);
    }

    private async Task ExecuteAsync(string sql, Guid userId, Guid branchId, bool maintenance = false)
    {
        await using var connection = new NpgsqlConnection(maintenance ? database.ConnectionString : database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("branch", branchId);
        await command.ExecuteNonQueryAsync();
    }
}
