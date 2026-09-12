using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;

namespace Dealership.Infrastructure.Accounts;

internal static class AccountPictureRemovals
{
    // The picture update is saved first because the removal trigger verifies that the picture is already removed.
    public static async Task<AccountPictureRemoval> RecordAsync(DealershipDbContext database, AccountPicture picture,
        AccountPictureRemovalReason reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        picture.MarkRemoved(now);
        await database.SaveChangesAsync(cancellationToken);
        var removal = new AccountPictureRemoval
        {
            PictureId = picture.Id,
            Reason = reason,
            EligibleAt = now,
            RequestedAt = now,
            NextAttemptAt = now
        };
        database.AccountPictureRemovals.Add(removal);
        await database.SaveChangesAsync(cancellationToken);
        return removal;
    }
}
