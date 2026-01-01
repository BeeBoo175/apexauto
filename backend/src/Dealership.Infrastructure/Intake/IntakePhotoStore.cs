using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class IntakePhotoStore(DealershipDbContext database, TimeProvider clock) : IIntakePhotoStore
{
    public async Task<PhotoPreparation> PrepareAsync(Guid ownerId, Guid id, PhotoAngle angle, PhotoContent content,
        TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize the global upload identity, including two different owners racing to claim it.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);

        if (!await IsVerifiedOwnerAsync(ownerId, lockUser: true, cancellationToken))
        {
            return new PhotoPreparation(PhotoStatus.Forbidden);
        }

        var existing = await database.IntakePhotos.SingleOrDefaultAsync(photo => photo.Id == id, cancellationToken);

        if (existing is not null)
        {
            return existing.Matches(ownerId, angle, content)
                ? await ClassifyAsync(existing, cancellationToken)
                : new PhotoPreparation(PhotoStatus.Conflict);
        }

        var photo = new IntakePhoto(id, ownerId, angle, content, ReceiptTime(), lifetime);
        database.IntakePhotos.Add(photo);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PhotoPreparation(PhotoStatus.Pending, photo);
    }

    public async Task<PhotoPreparation> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        if (!await IsVerifiedOwnerAsync(ownerId, lockUser: false, cancellationToken))
        {
            return new PhotoPreparation(PhotoStatus.Forbidden);
        }

        var photo = await database.IntakePhotos.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.OwnerId == ownerId, cancellationToken);
        return photo is null ? new PhotoPreparation(PhotoStatus.NotFound) : await ClassifyAsync(photo, cancellationToken);
    }

    public async Task<PhotoPreparation> ConfirmAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (!await IsVerifiedOwnerAsync(ownerId, lockUser: true, cancellationToken))
        {
            return new PhotoPreparation(PhotoStatus.Forbidden);
        }

        var photo = await database.IntakePhotos.FromSqlInterpolated(
            $"SELECT * FROM \"IntakePhotos\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return new PhotoPreparation(PhotoStatus.NotFound);
        }

        // Prepare may have tracked an earlier pending version in this same request scope.
        await database.Entry(photo).ReloadAsync(cancellationToken);
        var removal = await database.PhotoRemovals.AsNoTracking().SingleOrDefaultAsync(item => item.PhotoId == id, cancellationToken);

        if (removal is not null)
        {
            return Removed(photo, removal);
        }

        var now = ReceiptTime();

        if (photo.IsExpired(now))
        {
            return new PhotoPreparation(PhotoStatus.NotFound);
        }

        photo.ConfirmAvailable(now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PhotoPreparation(PhotoStatus.Available, photo);
    }

    private async Task<PhotoPreparation> ClassifyAsync(IntakePhoto photo, CancellationToken cancellationToken)
    {
        var removal = await database.PhotoRemovals.AsNoTracking().SingleOrDefaultAsync(item => item.PhotoId == photo.Id, cancellationToken);

        if (removal is not null)
        {
            return Removed(photo, removal);
        }

        if (photo.IsExpired(clock.GetUtcNow()) &&
            !await database.AppointmentPhotos.AnyAsync(item => item.PhotoId == photo.Id, cancellationToken))
        {
            return new PhotoPreparation(PhotoStatus.NotFound);
        }

        return new PhotoPreparation(photo.AvailableAt is null ? PhotoStatus.Pending : PhotoStatus.Available, photo);
    }

    private static PhotoPreparation Removed(IntakePhoto photo, PhotoRemoval removal)
    {
        var status = removal.DeletedAt is null ? PhotoStatus.DeletionPending : PhotoStatus.Deleted;
        return new PhotoPreparation(status, photo, removal);
    }

    private DateTimeOffset ReceiptTime()
    {
        // PostgreSQL stores microseconds; the first response must match persisted replay receipts exactly.
        var ticks = clock.GetUtcNow().UtcTicks;
        return new DateTimeOffset(ticks - ticks % 10, TimeSpan.Zero);
    }

    private async Task<bool> IsVerifiedOwnerAsync(Guid ownerId, bool lockUser, CancellationToken cancellationToken)
    {
        return await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser).AnyAsync(cancellationToken);
    }
}
