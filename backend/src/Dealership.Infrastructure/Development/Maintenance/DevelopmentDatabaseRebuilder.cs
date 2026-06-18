using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Development.Maintenance;

public sealed class DevelopmentDatabaseRebuilder(DealershipDbContext database, IAmazonS3? objects = null,
    PhotoStorageSettings? storage = null)
{
    public async Task RebuildAsync(DevelopmentFixtureTarget target, string? confirmation,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        var databaseName = new NpgsqlConnectionStringBuilder(target.ConnectionString).Database;

        if (confirmation != $"rebuild {databaseName}")
        {
            throw new InvalidOperationException("Database rebuild requires the exact typed target confirmation.");
        }

        if (objects is not null && storage is not null)
        {
            storage.Validate();
            if (!new Uri(storage.ServiceURL).IsLoopback)
            {
                throw new InvalidOperationException("Development storage rebuild requires explicitly configured local object storage.");
            }
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, deadline.Token);
        await using var permissions = new NpgsqlCommand(
            "SELECT rolsuper OR rolcreatedb FROM pg_roles WHERE rolname = current_user", lockConnection);

        if (await permissions.ExecuteScalarAsync(deadline.Token) is not true)
        {
            throw new InvalidOperationException("Database rebuild requires maintenance credentials able to recreate the database.");
        }

        await ClearLocalStorageAsync(deadline.Token);

        // The lock lives in postgres so it survives removal and recreation of the target database.
        await database.Database.EnsureDeletedAsync(deadline.Token);
        await database.Database.MigrateAsync(deadline.Token);
        database.ChangeTracker.Clear();
    }

    private async Task ClearLocalStorageAsync(CancellationToken cancellationToken)
    {
        if (objects is null || storage is null)
        {
            return;
        }

        try
        {
            ListObjectsV2Response page;
            do
            {
                // Delete each bounded first page so no continuation token depends on removed keys.
                page = await objects.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = storage.BucketName,
                    MaxKeys = 1000
                }, cancellationToken);
                if (page.S3Objects is not { Count: > 0 })
                {
                    break;
                }

                var deleted = await objects.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = storage.BucketName,
                    Objects = [.. page.S3Objects.Select(item => new KeyVersion { Key = item.Key })]
                }, cancellationToken);
                if (deleted.DeleteErrors is { Count: > 0 })
                {
                    throw new InvalidOperationException("Local object storage cleanup was incomplete.");
                }
            }
            while (page.IsTruncated == true);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound &&
            exception.ErrorCode == "NoSuchBucket")
        {
        }
    }
}
