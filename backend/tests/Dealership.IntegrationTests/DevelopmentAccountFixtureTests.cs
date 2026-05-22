using System.Diagnostics;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class DevelopmentAccountFixtureTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task AccountsOnlyCommandNeedsNoStorageAndPreservesEmptyBusinessTablesAcrossRestartAndDrift()
    {
        Assert.Equal(2, await CommandAsync("review-accounts"));
        Assert.Equal(0, await CommandAsync("seed-accounts"));
        using var provider = fixture.CreateServices();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal(20, await database.Users.CountAsync());
        Assert.Equal(6, await database.CustomerProfiles.CountAsync());
        Assert.Equal(3, await database.Branches.CountAsync());
        Assert.False(await database.Users.AnyAsync(item => !item.EmailConfirmed && item.Role == AccountRoles.Customer));
        await AssertBusinessTablesEmptyAsync();

        foreach (var email in DevelopmentFixtureManifest.Customers.Select(item => item.Registration.Email)
            .Concat(DevelopmentFixtureManifest.Staff.Select(item => item.Email)))
        {
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<AuthenticationService>()
                .LoginAsync(new(Guid.NewGuid(), email, DevelopmentFixtureManifest.Password), CancellationToken.None));
        }

        var before = await SnapshotAsync(database);
        Assert.Equal(0, await CommandAsync("review-accounts"));
        Assert.Equal(0, await CommandAsync("seed-accounts"));
        Assert.Equal(before, await SnapshotAsync(database));
        var branch = DevelopmentFixtureManifest.Branches[0];
        var administrator = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.Administrator);
        await database.Branches.Where(item => item.Id == branch.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Phone, "+60355659876"));
        await database.Users.Where(item => item.Id == administrator.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        var changed = await SnapshotAsync(database);
        Assert.Equal(2, await CommandAsync("review-accounts"));
        Assert.Equal(2, await CommandAsync("seed-accounts"));
        Assert.Equal(changed, await SnapshotAsync(database));
        await AssertBusinessTablesEmptyAsync();
    }

    private async Task<int> CommandAsync(string operation)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var command = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            $"../../../../../tools/DevelopmentFixtures/bin/{configuration}/net10.0/DevelopmentFixtures.dll"));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(operation);
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["POSTGRES_DB"] = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnectionString).Database;
        start.Environment["ConnectionStrings__DefaultConnection"] = fixture.RuntimeConnectionString;
        // This mode must construct and run without registering or contacting object storage.
        start.Environment["AWS__ServiceURL"] = "unavailable";
        start.Environment["AWS__BucketName"] = string.Empty;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(output, error);
            Assert.True(process.ExitCode is 0 or 2, $"{operation}: {await error}");
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private async Task AssertBusinessTablesEmptyAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var tables = new NpgsqlCommand("""
            SELECT tablename FROM pg_tables WHERE schemaname = 'public'
                AND tablename NOT IN ('__EFMigrationsHistory', 'Branches', 'AspNetUsers', 'CustomerProfiles',
                    'EmailVerificationChallenges', 'AccountSessions') ORDER BY tablename
            """, connection);
        var names = new List<string>();
        await using (var reader = await tables.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        using var identifiers = new NpgsqlCommandBuilder();
        foreach (var name in names)
        {
            await using var query = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {identifiers.QuoteIdentifier(name)})", connection);
            Assert.False((bool)(await query.ExecuteScalarAsync())!, $"Accounts-only seeding populated {name}");
        }
    }

    private static async Task<string> SnapshotAsync(DealershipDbContext database) => JsonSerializer.Serialize(new
    {
        Branches = await database.Branches.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Users = await database.Users.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Profiles = await database.CustomerProfiles.AsNoTracking().OrderBy(item => item.UserId).ToArrayAsync(),
        Verification = await database.EmailVerificationChallenges.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
        Sessions = await database.AccountSessions.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
    });
}
