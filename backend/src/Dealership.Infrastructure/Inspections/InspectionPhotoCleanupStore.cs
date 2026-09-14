using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionPhotoCleanupStore(DealershipDbContext database, IOptions<PhotoCleanupSettings> settings)
    : IPhotoCleanupSource
{
    public async Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var eligible = database.InspectionPhotos.Where(photo => photo.ExpiresAt <= now &&
            !database.InspectionDraftPhotos.Any(link => link.PhotoId == photo.Id) &&
            !database.InspectionPhotoRemovals.Any(removal => removal.PhotoId == photo.Id)).Select(photo => photo.Id);
        var retries = database.InspectionPhotoRemovals.Where(removal => removal.DeletedAt == null &&
            removal.NextAttemptAt <= now && (removal.ClaimExpiresAt == null || removal.ClaimExpiresAt <= now))
            .Select(removal => removal.PhotoId);
        return await eligible.Union(retries).Order().Take(settings.Value.BatchSize).ToArrayAsync(cancellationToken);
    }

    public async Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var photo = await database.InspectionPhotos.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionPhotos\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            var removal = await database.InspectionPhotoRemovals.SingleOrDefaultAsync(item => item.PhotoId == id, cancellationToken);
            // A retention receipt already proved its eligibility, although the photograph is attached to a signed save.
            var retained = removal?.Reason == InspectionPhotoRemovalReason.RetentionExpired;
            if (photo is null || !retained && (photo.ExpiresAt > now ||
                await database.InspectionDraftPhotos.AnyAsync(link => link.PhotoId == id, cancellationToken)))
            {
                return null;
            }

            if (removal is { DeletedAt: not null } || removal?.NextAttemptAt > now || removal?.ClaimExpiresAt > now)
            {
                return null;
            }

            if (removal is null)
            {
                removal = new InspectionPhotoRemoval { PhotoId = id, EligibleAt = photo.ExpiresAt, RequestedAt = now };
                database.InspectionPhotoRemovals.Add(removal);
            }

            removal.Attempts = Math.Min(removal.Attempts, int.MaxValue - 1) + 1;
            removal.ClaimId = Guid.NewGuid();
            removal.ClaimExpiresAt = now.AddMinutes(1);
            removal.NextAttemptAt = now.AddMinutes(Math.Min(30, Math.Pow(2, Math.Min(removal.Attempts - 1, 5))));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(photo, removal.ClaimId.Value);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        await database.InspectionPhotoRemovals.Where(item => item.PhotoId == photoId && item.ClaimId == claimId &&
                item.ClaimExpiresAt > now && item.DeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, now), cancellationToken);
    }
}
