using Dealership.Application.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Photos;

public sealed class PhotoTransferLock(DealershipDbContext database) : IPhotoTransferLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid photoId, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        return (await OpenAsync(photoId, wait: true, deadline.Token))!;
    }

    public Task<IAsyncDisposable?> TryAcquireAsync(Guid photoId, CancellationToken cancellationToken)
    {
        return OpenAsync(photoId, wait: false, cancellationToken);
    }

    private async Task<IAsyncDisposable?> OpenAsync(Guid photoId, bool wait, CancellationToken cancellationToken)
    {
        // A non-pooled session keeps the lock across provider I/O and releases it even after a process crash.
        var settings = new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString())
        {
            Pooling = false,
            Timeout = 10,
            CommandTimeout = 10
        };
        var connection = new NpgsqlConnection(settings.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            var sql = wait
                ? "SELECT pg_advisory_lock(hashtextextended(@key, 0))"
                : "SELECT pg_try_advisory_lock(hashtextextended(@key, 0))";
            await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = wait ? 0 : 10 };
            command.Parameters.AddWithValue("key", "photo-transfer:" + photoId);

            var acquired = await command.ExecuteScalarAsync(cancellationToken);

            if (wait || acquired is true)
            {
                return connection;
            }

            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
