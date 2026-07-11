using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class AccountPictureStore(DealershipDbContext database, TimeProvider clock) : IAccountPictureStore
{
    public async Task<AccountPicture?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await database.AccountPictures.AsNoTracking()
            .SingleOrDefaultAsync(picture => picture.UserId == userId && picture.RemovedAt == null, cancellationToken);
    }

    public async Task<AccountPicture?> ReplaceAsync(AccountPicture picture, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var previous = await database.AccountPictures.SingleOrDefaultAsync(
            existing => existing.UserId == picture.UserId && existing.RemovedAt == null, cancellationToken);

        if (previous is not null)
        {
            previous.MarkRemoved(Now());
            await database.SaveChangesAsync(cancellationToken);
        }

        database.AccountPictures.Add(picture);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return previous;
    }

    public async Task<AccountPicture?> RemoveAsync(Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        var existing = await database.AccountPictures.SingleOrDefaultAsync(
            picture => picture.UserId == userId && picture.RemovedAt == null, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        var removedAt = Now();
        existing.MarkRemoved(removedAt);
        if (actorId != userId)
        {
            database.AccountEvents.Add(new AccountEvent(Guid.NewGuid(), userId, actorId,
                AccountEventKind.PictureRemoved, null, null, removedAt));
        }

        await database.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private DateTimeOffset Now() => UtcTime.TruncateToMicroseconds(clock.GetUtcNow());
}
