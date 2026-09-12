using System.Security.Cryptography;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The changes every erased account shares, staff or customer, run inside the caller's locked transaction. The
// identity closes first because the minimization guards of the dependent tables are bound to the closed state.
internal static class AccountErasure
{
    public static async Task CloseIdentityAsync(DealershipDbContext database, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Pictures first: the removal trigger needs the picture row saved as removed before its receipt.
        var pictures = await database.AccountPictures.FromSqlInterpolated(
            $"SELECT * FROM \"AccountPictures\" WHERE \"UserId\" = {userId} AND \"RemovedAt\" IS NULL ORDER BY (\"AvailableAt\" IS NULL), \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        foreach (var picture in pictures)
        {
            if (picture.AvailableAt is null)
            {
                picture.ConfirmAvailable(now);
            }

            await AccountPictureRemovals.RecordAsync(database, picture, AccountPictureRemovalReason.Removed, now,
                cancellationToken);
        }

        // A random stamp, never NULL, because NULL stamps compare equal.
        var stamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await database.Users.Where(user => user.Id == userId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.ClosedAt, now)
            .SetProperty(user => user.IsActive, false)
            .SetProperty(user => user.SuspendedAt, (DateTimeOffset?)null)
            .SetProperty(user => user.PasswordHash, (string?)null)
            .SetProperty(user => user.EmailConfirmed, false)
            .SetProperty(user => user.Email, (string?)null)
            .SetProperty(user => user.NormalizedEmail, (string?)null)
            .SetProperty(user => user.UserName, (string?)null)
            .SetProperty(user => user.NormalizedUserName, (string?)null)
            .SetProperty(user => user.AccountUsername, (string?)null)
            .SetProperty(user => user.PhoneNumber, (string?)null)
            .SetProperty(user => user.PhoneNumberConfirmed, false)
            .SetProperty(user => user.SecurityStamp, stamp), cancellationToken);

        await AccountAccessRevocation.RevokeAsync(database, userId, now, cancellationToken);
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == userId && challenge.InvalidatedAt == null &&
                challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now),
                cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "EmailVerificationChallenges" SET "RecipientEmail" = NULL
            WHERE "UserId" = {userId} AND "RecipientEmail" IS NOT NULL
            """, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "PasswordResetChallenges" SET "RecipientEmail" = NULL
            WHERE "UserId" = {userId} AND "RecipientEmail" IS NOT NULL
            """, cancellationToken);
        // Pictures whose objects are already gone drop their hash now, the rest when the worker confirms deletion.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AccountPictures" p SET "Sha256" = NULL
            WHERE p."UserId" = {userId} AND p."Sha256" IS NOT NULL AND EXISTS
                (SELECT 1 FROM "AccountPictureRemovals" r WHERE r."PictureId" = p."Id" AND r."DeletedAt" IS NOT NULL)
            """, cancellationToken);
    }

    // The audit receipt is the replay key. Its reason and values are minimized in the same transaction, so only the
    // fact and time of the erasure remain.
    public static async Task RecordAsync(DealershipDbContext database, Guid userId, Guid operationId, Guid actorId,
        string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        database.AccountEvents.Add(new AccountEvent(operationId, userId, actorId, AccountEventKind.Erased, reason, null,
            now));
        await database.SaveChangesAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AccountEvents" SET "Reason" = NULL, "Changes" = NULL, "MinimizedAt" = {now}
            WHERE "TargetId" = {userId} AND "MinimizedAt" IS NULL AND ("Reason" IS NOT NULL OR "Changes" IS NOT NULL)
            """, cancellationToken);
    }
}
