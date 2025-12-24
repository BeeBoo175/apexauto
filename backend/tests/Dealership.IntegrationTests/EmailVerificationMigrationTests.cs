using Dealership.Application;
using Dealership.Application.Accounts;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class EmailVerificationMigrationTests(RegistrationDatabase fixture) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RemovingResendFlagPreservesChallengesAndRollbackReconstructsIt()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var registered = await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(DevelopmentFixtureManifest.Customer, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, registered.Status);
        var userId = await database.CustomerProfiles.Select(profile => profile.UserId).SingleAsync();
        var verification = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        var resendKey = Guid.NewGuid();
        Assert.Equal(VerificationStatus.Accepted,
            (await verification.ResendAsync(userId, resendKey, CancellationToken.None)).Status);
        var before = await database.EmailVerificationChallenges.AsNoTracking().OrderBy(challenge => challenge.Id)
            .Select(challenge => new { challenge.Id, challenge.UserId, challenge.ResendKey, challenge.ExpiresAt, challenge.InvalidatedAt })
            .ToListAsync();
        Assert.Equal(2, before.Count);

        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261014152056_EmailVerification");
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var mismatches = new NpgsqlCommand("""
                SELECT count(*) FROM "EmailVerificationChallenges"
                WHERE "IsResend" <> ("ResendKey" IS NOT NULL)
                """, connection);
            Assert.Equal(0L, await mismatches.ExecuteScalarAsync());
        }

        await migrator.MigrateAsync();
        var after = await database.EmailVerificationChallenges.AsNoTracking().OrderBy(challenge => challenge.Id)
            .Select(challenge => new { challenge.Id, challenge.UserId, challenge.ResendKey, challenge.ExpiresAt, challenge.InvalidatedAt })
            .ToListAsync();
        Assert.Equal(before, after);
        Assert.Equal(VerificationStatus.Accepted,
            (await verification.ResendAsync(userId, resendKey, CancellationToken.None)).Status);
        Assert.Equal(VerificationStatus.RateLimited,
            (await verification.ResendAsync(userId, Guid.NewGuid(), CancellationToken.None)).Status);
    }
}
