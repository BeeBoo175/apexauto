using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

public sealed record InspectionPhotoDeletionClaim(InspectionPhoto Photo, Guid ClaimId);

public sealed class InspectionPhotoCleanupStore(DealershipDbContext database)
{
    public async Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = await DatabaseTimeAsync(cancellationToken);
        var eligible = database.InspectionPhotos.Where(photo => photo.ExpiresAt <= now &&
            !database.InspectionDraftPhotos.Any(link => link.PhotoId == photo.Id) &&
            !database.InspectionPhotoRemovals.Any(removal => removal.PhotoId == photo.Id)).Select(photo => photo.Id);
        var retries = database.InspectionPhotoRemovals.Where(removal => removal.DeletedAt == null &&
            removal.NextAttemptAt <= now && (removal.ClaimExpiresAt == null || removal.ClaimExpiresAt <= now))
            .Select(removal => removal.PhotoId);
        return await eligible.Union(retries).Order().Take(10).ToArrayAsync(cancellationToken);
    }

    public async Task<InspectionPhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var photo = await database.InspectionPhotos.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionPhotos\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            var now = await DatabaseTimeAsync(cancellationToken);
            if (photo is null || photo.ExpiresAt > now ||
                await database.InspectionDraftPhotos.AnyAsync(link => link.PhotoId == id, cancellationToken))
            {
                return null;
            }

            var removal = await database.InspectionPhotoRemovals.SingleOrDefaultAsync(item => item.PhotoId == id, cancellationToken);
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
        var now = await DatabaseTimeAsync(cancellationToken);
        await database.InspectionPhotoRemovals.Where(item => item.PhotoId == photoId && item.ClaimId == claimId &&
                item.ClaimExpiresAt > now && item.DeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, now), cancellationToken);
    }

    private Task<DateTimeOffset> DatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
    }
}
