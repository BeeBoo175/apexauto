using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

internal static class DatabaseClock
{
    // The one authoritative instant for reads and commands that decide expiry, so both judge deadlines alike.
    public static Task<DateTimeOffset> DatabaseNowAsync(this DealershipDbContext database,
        CancellationToken cancellationToken) =>
        database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
}
