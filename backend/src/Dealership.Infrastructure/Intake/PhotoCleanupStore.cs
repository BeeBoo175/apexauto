using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed record PhotoDeletionClaim(IntakePhoto Photo, Guid ClaimId);

public sealed class PhotoCleanupStore(DealershipDbContext database, TimeProvider clock)
{
    public async Task<IReadOnlyList<Guid>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var eligible = database.IntakePhotos.Where(photo =>
            !database.PhotoRemovals.Any(removal => removal.PhotoId == photo.Id) &&
            ((!database.AppointmentPhotos.Any(link => link.PhotoId == photo.Id) && photo.ExpiresAt <= now) ||
                database.AppointmentPhotos.Any(link => link.PhotoId == photo.Id &&
                    database.AppointmentCancellations.Any(cancellation => cancellation.AppointmentId == link.AppointmentId &&
                        cancellation.PhotosExpireAt <= now))))
            .Select(photo => photo.Id);
        var retries = database.PhotoRemovals.Where(removal => removal.DeletedAt == null && removal.NextAttemptAt <= now &&
                (removal.ClaimExpiresAt == null || removal.ClaimExpiresAt <= now))
            .Select(removal => removal.PhotoId);
        return await eligible.Union(retries).OrderBy(id => id).Take(10).ToArrayAsync(cancellationToken);
    }

    public async Task<PhotoDeletionClaim?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var photo = await database.IntakePhotos.FromSqlInterpolated(
            $"SELECT * FROM \"IntakePhotos\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return null;
        }

        var now = await ReadDatabaseTimeAsync(cancellationToken);
        var eligibility = await GetEligibilityAsync(photo, cancellationToken);

        if (eligibility is null || now < eligibility.Value.At)
        {
            return null;
        }

        var removal = await database.PhotoRemovals.SingleOrDefaultAsync(item => item.PhotoId == id, cancellationToken);

        if (removal is { DeletedAt: not null } || removal?.NextAttemptAt > now || removal?.ClaimExpiresAt > now)
        {
            return null;
        }

        if (removal is null)
        {
            removal = new PhotoRemoval
            {
                PhotoId = id,
                Reason = eligibility.Value.Reason,
                EligibleAt = eligibility.Value.At,
                RequestedAt = now
            };
            database.PhotoRemovals.Add(removal);
        }

        removal.Attempts = Math.Min(removal.Attempts, int.MaxValue - 1) + 1;
        removal.ClaimId = Guid.NewGuid();
        removal.ClaimExpiresAt = now.AddMinutes(1);
        removal.NextAttemptAt = now.AddMinutes(Math.Min(30, Math.Pow(2, Math.Min(removal.Attempts - 1, 5))));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(removal).State = EntityState.Detached;
        return new PhotoDeletionClaim(photo, removal.ClaimId.Value);
    }

    public async Task MarkDeletedAsync(Guid photoId, Guid claimId, CancellationToken cancellationToken)
    {
        var now = await ReadDatabaseTimeAsync(cancellationToken);
        await database.PhotoRemovals.Where(item => item.PhotoId == photoId && item.ClaimId == claimId && item.DeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.DeletedAt, now), cancellationToken);
    }

    private Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
    }

    private async Task<(PhotoRemovalReason Reason, DateTimeOffset At)?> GetEligibilityAsync(IntakePhoto photo,
        CancellationToken cancellationToken)
    {
        var appointment = await database.AppointmentPhotos.AsNoTracking()
            .Where(link => link.PhotoId == photo.Id).Select(link => (Guid?)link.AppointmentId).SingleOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return (PhotoRemovalReason.UnsubmittedExpired, photo.ExpiresAt);
        }

        var expiresAt = await database.AppointmentCancellations.AsNoTracking()
            .Where(item => item.AppointmentId == appointment).Select(item => (DateTimeOffset?)item.PhotosExpireAt)
            .SingleOrDefaultAsync(cancellationToken);

        if (expiresAt is null)
        {
            return null;
        }

        return (PhotoRemovalReason.CancelledRetentionExpired, expiresAt.Value);
    }
}
