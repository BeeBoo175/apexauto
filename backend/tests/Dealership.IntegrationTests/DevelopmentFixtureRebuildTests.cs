using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Dealership.Domain.Branches;
using Dealership.Application.Accounts;
using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Development.Maintenance;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

[Trait("Category", "SeedPipeline")]
public sealed class DevelopmentFixtureRebuildTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task RebuildRequiresExactConfirmationClearsLocalBucketAndRecreatesMigratedEmptyDatabase()
    {
        var databaseName = new NpgsqlConnectionStringBuilder(fixture.Database.ConnectionString).Database!;
        var target = DevelopmentFixtureTarget.Validate("Development", databaseName, fixture.Database.ConnectionString);
        await using var provider = CreateProvider(fixture.Database.ConnectionString, fixture.BucketName);
        await using var scope = provider.CreateAsyncScope();
        var rebuilder = scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseRebuilder>();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var objects = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Password123!", "940215140865",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor");
        var registered = await scope.ServiceProvider.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None);
        Assert.Equal(RegistrationStatus.Succeeded, registered.Status);
        database.Branches.Add(new Branch(Guid.NewGuid(), "ApexAuto Glenmarie", "12 Jalan Pengaturcara U1/51A",
            "Shah Alam", "Selangor", "+60355651234", DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("stale-photo-bytes")))
        {
            await objects.PutObjectAsync(new PutObjectRequest
            {
                BucketName = fixture.BucketName,
                Key = "intake/photos/stale-object",
                InputStream = content,
                ContentType = "image/jpeg",
                UseChunkEncoding = false
            });
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => rebuilder.RebuildAsync(
            target, $"rebuild {databaseName}_different", CancellationToken.None));
        await using (var remoteProvider = CreateProvider(fixture.Database.ConnectionString, fixture.BucketName,
            "https://storage.example.invalid"))
        {
            await using var remoteScope = remoteProvider.CreateAsyncScope();
            var remoteRebuilder = remoteScope.ServiceProvider.GetRequiredService<DevelopmentDatabaseRebuilder>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => remoteRebuilder.RebuildAsync(
                target, $"rebuild {databaseName}", CancellationToken.None));
        }

        Assert.Equal(1, await database.Users.CountAsync());
        Assert.Single((await objects.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = fixture.BucketName
        })).S3Objects);
        var runtimeTarget = DevelopmentFixtureTarget.Validate("Development", databaseName, fixture.Database.RuntimeConnectionString);
        var runtimeRole = new NpgsqlConnectionStringBuilder(fixture.Database.RuntimeConnectionString).Username!;
        using var identifiers = new NpgsqlCommandBuilder();
        // A database owner can drop its database while still lacking CREATEDB for recreation.
        await using var ownerConnection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await ownerConnection.OpenAsync();
        await using var changeOwner = new NpgsqlCommand(
            $"ALTER DATABASE {identifiers.QuoteIdentifier(databaseName)} OWNER TO {identifiers.QuoteIdentifier(runtimeRole)}",
            ownerConnection);
        await changeOwner.ExecuteNonQueryAsync();
        await ownerConnection.CloseAsync();

        await using (var runtimeProvider = CreateProvider(fixture.Database.RuntimeConnectionString, fixture.BucketName))
        {
            await using var runtimeScope = runtimeProvider.CreateAsyncScope();
            var runtimeRebuilder = runtimeScope.ServiceProvider.GetRequiredService<DevelopmentDatabaseRebuilder>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtimeRebuilder.RebuildAsync(
                runtimeTarget, $"rebuild {databaseName}", CancellationToken.None));
        }

        Assert.Equal(1, await database.Users.CountAsync());
        Assert.Single((await objects.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = fixture.BucketName
        })).S3Objects);
        await rebuilder.RebuildAsync(target, $"rebuild {databaseName}", CancellationToken.None);
        Assert.Equal(0, await database.Users.CountAsync());
        Assert.Equal(0, await database.Branches.CountAsync());
        Assert.Equal(0, await database.CustomerProfiles.CountAsync());
        Assert.Empty((await objects.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = fixture.BucketName
        })).S3Objects ?? []);
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
    }

    private static ServiceProvider CreateProvider(string connection, string bucket, string? serviceUrl = null)
    {
        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["AWS:BucketName"] = bucket
        };
        if (serviceUrl is not null)
        {
            overrides["AWS:ServiceURL"] = serviceUrl;
        }

        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(overrides).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddS3Storage(configuration);
        services.AddScoped<DevelopmentDatabaseRebuilder>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
