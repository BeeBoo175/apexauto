using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Vehicles;

// Deletes only the objects of uploads that were never confirmed and never referenced. Confirmed library photos and
// photos saved in any revision are never eligible, and deselecting a photo is not erasure.
public sealed class ListingPhotoCleanupStore(DealershipDbContext database, IOptions<PhotoCleanupSettings> settings)
    : IPhotoCleanupSource
{
    public async Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var abandonedBefore = now - Lifetime;
        var eligible = database.ListingPhotos.Where(photo => photo.AvailableAt == null &&
            photo.CreatedAt <= abandonedBefore &&
            !database.ListingRevisionPhotos.Any(link => link.PhotoId == photo.Id) &&
            !database.ListingPhotoRemovals.Any(removal => removal.PhotoId == photo.Id)).Select(photo => photo.Id);
        var retries = database.ListingPhotoRemovals.Where(removal => removal.DeletedAt == null &&
            removal.NextAttemptAt <= now && (removal.ClaimExpiresAt == null || removal.ClaimExpiresAt <= now))
            .Select(removal => removal.PhotoId);
        return await eligible.Union(retries).Order().Take(settings.Value.BatchSize).ToArrayAsync(cancellationToken);
    }

    public async Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var photo = await database.ListingPhotos.FromSqlInterpolated(
                $"SELECT * FROM \"ListingPhotos\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            var removal = photo is null
                ? null
                : await database.ListingPhotoRemovals.SingleOrDefaultAsync(item => item.PhotoId == id, cancellationToken);

            if (photo is null || removal is { DeletedAt: not null } || removal?.NextAttemptAt > now ||
                removal?.ClaimExpiresAt > now)
            {
                return null;
            }

            if (removal is null)
            {
                var eligibleAt = photo.CreatedAt + Lifetime;

                if (photo.AvailableAt is not null || now < eligibleAt ||
                    await database.ListingRevisionPhotos.AnyAsync(link => link.PhotoId == id, cancellationToken))
                {
                    return null;
                }

                removal = new ListingPhotoRemoval { PhotoId = id, EligibleAt = eligibleAt, RequestedAt = now };
                database.ListingPhotoRemovals.Add(removal);
            }

            var claimId = removal.StartAttempt(now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PhotoDeletionClaim(photo, claimId);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        await database.ListingPhotoRemovals.Where(item => item.PhotoId == photoId && item.ClaimId == claimId &&
                item.ClaimExpiresAt > now && item.DeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, now), cancellationToken);
    }

    private TimeSpan Lifetime => TimeSpan.FromHours(settings.Value.PendingUploadLifetimeHours);
}
