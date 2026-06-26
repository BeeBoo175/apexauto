using Dealership.Application.Photos;
using Dealership.Domain.Accounts;
using Dealership.Domain.Photos;

namespace Dealership.Application.Accounts;

// A picture is visible to staff, to its owner and to customers whose records its owner handles; only the owner or an Administrator may remove it.
public sealed class AccountPictureService(IAccountProfileQuery profiles, IAccountPictureStore pictures,
    IPhotoImageValidator validator, IPhotoThumbnailer thumbnailer, PhotoTransferService transfers,
    IPhotoTransferLock locks, IPhotoStorage storage, IRecordHandlerQuery handlers, TimeProvider clock)
{
    public async Task<AccountPictureResult> UploadAsync(Guid actorId, string contentType, Stream content,
        CancellationToken cancellationToken)
    {
        if (!PhotoContent.IsSupportedContentType(contentType))
        {
            return new AccountPictureResult(AccountPictureStatus.Invalid);
        }

        if (await profiles.GetProfileAsync(actorId, cancellationToken) is null)
        {
            return new AccountPictureResult(AccountPictureStatus.Forbidden);
        }

        if (await validator.ValidateAsync(content, contentType, cancellationToken) is null)
        {
            return new AccountPictureResult(AccountPictureStatus.Invalid);
        }

        await using var thumbnail = new MemoryStream();
        var described = await thumbnailer.CreateAsync(content, thumbnail, AccountPicture.ThumbnailEdge, cancellationToken);

        if (described is null)
        {
            return new AccountPictureResult(AccountPictureStatus.Invalid);
        }

        var picture = new AccountPicture(Guid.NewGuid(), actorId, described, Truncated(clock.GetUtcNow()));
        AccountPicture? replaced = null;
        await transfers.UploadAsync(actorId, thumbnail,
            _ => Task.FromResult(picture), prepared => prepared,
            async token =>
            {
                replaced = await pictures.ReplaceAsync(picture, token);
                return picture;
            }, cancellationToken);
        await DeleteObjectAsync(replaced, cancellationToken);
        return new AccountPictureResult(AccountPictureStatus.Stored, picture.Id, picture.ContentType);
    }

    public async Task<AccountPictureResult> DownloadAsync(Guid viewerId, Guid ownerId, Stream destination,
        CancellationToken cancellationToken)
    {
        var viewer = await profiles.GetProfileAsync(viewerId, cancellationToken);

        if (viewer is null || (viewerId != ownerId && viewer.Role == AccountRoles.Customer &&
            !await handlers.HandlesCustomerAsync(ownerId, viewerId, cancellationToken)))
        {
            return new AccountPictureResult(AccountPictureStatus.Forbidden);
        }

        var found = await transfers.DownloadAsync(ownerId, destination,
            token => pictures.FindAsync(ownerId, token), picture => picture, cancellationToken);
        return found is null
            ? new AccountPictureResult(AccountPictureStatus.NotFound)
            : new AccountPictureResult(AccountPictureStatus.Available, found.Id, found.ContentType);
    }

    public async Task<AccountPictureResult> RemoveAsync(Guid actorId, Guid ownerId, CancellationToken cancellationToken)
    {
        var actor = await profiles.GetProfileAsync(actorId, cancellationToken);

        if (actor is null || (actorId != ownerId && actor.Role != AccountRoles.Administrator))
        {
            return new AccountPictureResult(AccountPictureStatus.Forbidden);
        }

        await using var transfer = await locks.AcquireAsync(ownerId, cancellationToken);
        var removed = await pictures.RemoveAsync(ownerId, actorId, cancellationToken);
        await DeleteObjectAsync(removed, cancellationToken);
        return new AccountPictureResult(removed is null ? AccountPictureStatus.NotFound : AccountPictureStatus.Removed);
    }

    // The row is the source of truth; an object that cannot be deleted now is unreachable and only wastes space.
    private async Task DeleteObjectAsync(AccountPicture? picture, CancellationToken cancellationToken)
    {
        if (picture is null)
        {
            return;
        }

        try
        {
            await storage.DeleteAsync(picture, cancellationToken);
        }
        catch (PhotoStorageException)
        {
            // Left for a later storage sweep; the account no longer references it.
        }
    }

    private static DateTimeOffset Truncated(DateTimeOffset value)
    {
        return new DateTimeOffset(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    }
}
