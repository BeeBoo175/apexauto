using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Accounts;

// Deletes the objects of removed, replaced and never-confirmed pictures. A confirmed current picture is never eligible.
public sealed class AccountPictureCleanupStore(DealershipDbContext database, IOptions<PhotoCleanupSettings> settings)
    : IPhotoCleanupSource
{
    public async Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var eligible = database.AccountPictures.Where(picture =>
            !database.AccountPictureRemovals.Any(removal => removal.PictureId == picture.Id) &&
            (picture.RemovedAt != null || (picture.AvailableAt == null && picture.ExpiresAt <= now)))
            .Select(picture => picture.Id);
        var retries = database.AccountPictureRemovals.Where(removal => removal.DeletedAt == null &&
            removal.NextAttemptAt <= now && (removal.ClaimExpiresAt == null || removal.ClaimExpiresAt <= now))
            .Select(removal => removal.PictureId);
        return await eligible.Union(retries).Order().Take(settings.Value.BatchSize).ToArrayAsync(cancellationToken);
    }

    public async Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var picture = await database.AccountPictures.FromSqlInterpolated(
                $"SELECT * FROM \"AccountPictures\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            var removal = picture is null
                ? null
                : await database.AccountPictureRemovals.SingleOrDefaultAsync(item => item.PictureId == id, cancellationToken);

            if (picture is null || removal is { DeletedAt: not null } || removal?.NextAttemptAt > now ||
                removal?.ClaimExpiresAt > now)
            {
                return null;
            }

            if (removal is null)
            {
                var eligibility = GetEligibility(picture);

                if (eligibility is null || now < eligibility.Value.At)
                {
                    return null;
                }

                removal = new AccountPictureRemoval
                {
                    PictureId = id,
                    Reason = eligibility.Value.Reason,
                    EligibleAt = eligibility.Value.At,
                    RequestedAt = now
                };
                database.AccountPictureRemovals.Add(removal);
            }

            removal.Attempts = Math.Min(removal.Attempts, int.MaxValue - 1) + 1;
            removal.ClaimId = Guid.NewGuid();
            removal.ClaimExpiresAt = now.AddMinutes(1);
            removal.NextAttemptAt = now.AddMinutes(Math.Min(30, Math.Pow(2, Math.Min(removal.Attempts - 1, 5))));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PhotoDeletionClaim(picture, removal.ClaimId.Value);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        await database.AccountPictureRemovals.Where(item => item.PictureId == photoId && item.ClaimId == claimId &&
                item.ClaimExpiresAt > now && item.DeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, now), cancellationToken);

        // The hash can identify the image, so a closed owner's picture drops it once the object is gone.
        await database.AccountPictures.Where(picture => picture.Id == photoId && picture.Sha256 != null &&
                database.Users.Any(user => user.Id == picture.UserId && user.ClosedAt != null) &&
                database.AccountPictureRemovals.Any(item => item.PictureId == photoId && item.DeletedAt != null))
            .ExecuteUpdateAsync(update => update.SetProperty(picture => picture.Sha256, (string?)null), cancellationToken);
    }

    private static (AccountPictureRemovalReason Reason, DateTimeOffset At)? GetEligibility(AccountPicture picture)
    {
        if (picture.RemovedAt is { } removedAt)
        {
            return (AccountPictureRemovalReason.Removed, removedAt);
        }

        return picture.AvailableAt is null
            ? (AccountPictureRemovalReason.UnconfirmedExpired, picture.ExpiresAt)
            : null;
    }
}
