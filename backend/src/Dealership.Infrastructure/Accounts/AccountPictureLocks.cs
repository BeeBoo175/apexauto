using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

internal static class AccountPictureLocks
{
    // Promotion, removal and closure of one account's pictures serialize here before any row lock, in the same order.
    public static Task LockOwnerAsync(DealershipDbContext database, Guid userId, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-picture:" + userId}, 0))", cancellationToken);
}
