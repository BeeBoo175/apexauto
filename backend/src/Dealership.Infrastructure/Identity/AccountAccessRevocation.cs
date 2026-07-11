using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

// Ends an account's current access inside the caller's transaction; each caller decides which parts its change needs.
internal static class AccountAccessRevocation
{
    public static Task<int> RevokeSessionsAsync(DealershipDbContext database, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        database.AccountSessions.Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);

    public static Task<int> InvalidateResetLinksAsync(DealershipDbContext database, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        database.PasswordResetChallenges
            .Where(challenge => challenge.UserId == userId && challenge.InvalidatedAt == null &&
                challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now),
                cancellationToken);

    public static async Task RevokeAsync(DealershipDbContext database, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await RevokeSessionsAsync(database, userId, now, cancellationToken);
        await InvalidateResetLinksAsync(database, userId, now, cancellationToken);
    }
}
