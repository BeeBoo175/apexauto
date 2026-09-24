using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Photos;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountPictureStore(DealershipDbContext database, TimeProvider clock, PhotoCleanupSignal cleanup)
    : IAccountPictureStore
{
    public async Task<AccountPicture?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await database.AccountPictures.AsNoTracking().Where(AccountPicture.Current)
            .SingleOrDefaultAsync(picture => picture.UserId == userId, cancellationToken);
    }

    public async Task<AccountPicturePreparation> FindAsync(Guid userId, Guid pictureId, CancellationToken cancellationToken)
    {
        var picture = await database.AccountPictures.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == pictureId && candidate.UserId == userId, cancellationToken);
        return picture is null
            ? new AccountPicturePreparation(AccountPictureStatus.NotFound)
            : await ClassifyAsync(picture, cancellationToken);
    }

    public async Task<AccountPicturePreparation> PrepareAsync(Guid userId, Guid pictureId, PhotoContent content,
        TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize the global upload identity, including two different owners racing to claim it.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"account-picture-upload:" + pictureId}, 0))", cancellationToken);

        if (!await LockUserAsync(userId, cancellationToken))
        {
            return new AccountPicturePreparation(AccountPictureStatus.Forbidden);
        }

        var existing = await database.AccountPictures.SingleOrDefaultAsync(picture => picture.Id == pictureId, cancellationToken);

        if (existing is not null)
        {
            return existing.Matches(userId, content)
                ? await ClassifyAsync(existing, cancellationToken)
                : new AccountPicturePreparation(AccountPictureStatus.Conflict);
        }

        var pending = new AccountPicture(pictureId, userId, content, ReceiptTime(), lifetime);
        database.AccountPictures.Add(pending);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cleanup.Request();
        return new AccountPicturePreparation(AccountPictureStatus.Pending, pending);
    }

    public async Task<AccountPicturePreparation> ConfirmAsync(Guid userId, Guid pictureId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AccountPictureLocks.LockOwnerAsync(database, userId, cancellationToken);

        if (!await LockUserAsync(userId, cancellationToken))
        {
            return new AccountPicturePreparation(AccountPictureStatus.Forbidden);
        }

        var picture = await LockPictureAsync(userId, pictureId, cancellationToken);

        if (picture is null)
        {
            return new AccountPicturePreparation(AccountPictureStatus.NotFound);
        }

        // Prepare may have tracked an earlier pending version in this same request scope.
        await database.Entry(picture).ReloadAsync(cancellationToken);
        var removal = await FindRemovalAsync(pictureId, cancellationToken);

        if (removal is not null)
        {
            return Removed(picture, removal);
        }

        if (picture.AvailableAt is not null)
        {
            return new AccountPicturePreparation(AccountPictureStatus.Available, picture);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);

        if (picture.IsExpired(now))
        {
            return new AccountPicturePreparation(AccountPictureStatus.NotFound);
        }

        await RetirePreviousAsync(userId, now, cancellationToken);
        picture.ConfirmAvailable(now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cleanup.Request();
        return new AccountPicturePreparation(AccountPictureStatus.Available, picture);
    }

    public async Task<AccountPicturePreparation> RemoveAsync(Guid userId, Guid pictureId, Guid actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AccountPictureLocks.LockOwnerAsync(database, userId, cancellationToken);

        if (!await LockUserAsync(userId, cancellationToken))
        {
            return new AccountPicturePreparation(AccountPictureStatus.NotFound);
        }

        var picture = await LockPictureAsync(userId, pictureId, cancellationToken);

        if (picture is null)
        {
            return new AccountPicturePreparation(AccountPictureStatus.NotFound);
        }

        var existing = await FindRemovalAsync(pictureId, cancellationToken);

        if (existing is not null)
        {
            // A stale target that was already removed or replaced answers its own receipt, never the current picture.
            return Removed(picture, existing);
        }

        if (picture.AvailableAt is null)
        {
            return new AccountPicturePreparation(AccountPictureStatus.Conflict);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var removal = await AccountPictureRemovals.RecordAsync(database, picture, AccountPictureRemovalReason.Removed, now, cancellationToken);

        if (actorId != userId)
        {
            database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), userId, actorId,
                AccountEventKind.PictureRemoved, null, null, now));
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        cleanup.Request();
        return Removed(picture, removal);
    }

    private async Task RetirePreviousAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = await database.AccountPictures.FromSqlInterpolated(
            $"SELECT * FROM \"AccountPictures\" WHERE \"UserId\" = {userId} AND \"RemovedAt\" IS NULL AND \"AvailableAt\" IS NOT NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (previous is not null)
        {
            await AccountPictureRemovals.RecordAsync(database, previous, AccountPictureRemovalReason.Replaced, now, cancellationToken);
        }
    }

    private async Task<AccountPicturePreparation> ClassifyAsync(AccountPicture picture,
        CancellationToken cancellationToken)
    {
        var removal = await FindRemovalAsync(picture.Id, cancellationToken);

        if (removal is not null)
        {
            return Removed(picture, removal);
        }

        if (picture.AvailableAt is not null)
        {
            return new AccountPicturePreparation(AccountPictureStatus.Available, picture);
        }

        return picture.IsExpired(clock.GetUtcNow())
            ? new AccountPicturePreparation(AccountPictureStatus.NotFound)
            : new AccountPicturePreparation(AccountPictureStatus.Pending, picture);
    }

    private async Task<AccountPictureRemoval?> FindRemovalAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        return await database.AccountPictureRemovals.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PictureId == pictureId, cancellationToken);
    }

    private static AccountPicturePreparation Removed(AccountPicture picture, AccountPictureRemoval removal)
    {
        var status = removal.DeletedAt is null ? AccountPictureStatus.DeletionPending : AccountPictureStatus.Deleted;
        return new AccountPicturePreparation(status, picture, removal);
    }

    private async Task<AccountPicture?> LockPictureAsync(Guid userId, Guid pictureId, CancellationToken cancellationToken)
    {
        return await database.AccountPictures.FromSqlInterpolated(
            $"SELECT * FROM \"AccountPictures\" WHERE \"Id\" = {pictureId} AND \"UserId\" = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    // A shared user lock lets pictures proceed together while excluding a command that rewrites the account row.
    private async Task<bool> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} AND \"ClosedAt\" IS NULL FOR SHARE")
            .AsNoTracking().AnyAsync(cancellationToken);
    }

    // PostgreSQL stores microseconds; the first response must match persisted replay receipts exactly.
    private DateTimeOffset ReceiptTime() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}
