using Dealership.Application.Photos;
using Dealership.Domain.Accounts;
using Dealership.Domain.Photos;

namespace Dealership.Application.Accounts;

// A picture is visible to staff, to its owner and to customers whose records its owner handles; only the owner or an Administrator may remove it.
// Objects are deleted by the shared cleanup worker from the durable removal receipt, never inline.
public sealed class AccountPictureService(IAccountProfileQuery profiles, IAccountPictureStore pictures,
    IPhotoImageValidator validator, IPhotoThumbnailer thumbnailer, PhotoTransferService transfers,
    IPhotoTransferLock locks, IRecordHandlerQuery handlers, AccountPicturePolicy policy)
{
    public async Task<AccountPictureResult> UploadAsync(Guid actorId, Guid pictureId, string contentType, Stream content,
        CancellationToken cancellationToken)
    {
        if (pictureId == Guid.Empty || !PhotoContent.IsSupportedContentType(contentType))
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

        return Result(await transfers.UploadAsync(pictureId, thumbnail,
            token => pictures.PrepareAsync(actorId, pictureId, described, policy.PendingLifetime, token),
            prepared => prepared.Status == AccountPictureStatus.Pending ? prepared.Picture : null,
            token => pictures.ConfirmAsync(actorId, pictureId, token), cancellationToken));
    }

    public async Task<AccountPictureResult> GetReceiptAsync(Guid actorId, Guid pictureId,
        CancellationToken cancellationToken)
    {
        return Result(await pictures.FindAsync(actorId, pictureId, cancellationToken));
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

        // A replacement can land between choosing the object and copying it; the second pass follows the new one.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var current = await pictures.FindAsync(ownerId, cancellationToken);

            if (current is null)
            {
                break;
            }

            var found = await transfers.DownloadAsync(current.Id, destination,
                token => pictures.FindAsync(ownerId, token),
                picture => picture?.Id == current.Id ? picture : null, cancellationToken);

            if (found?.Id == current.Id)
            {
                return new AccountPictureResult(AccountPictureStatus.Available, Receipt(found));
            }

            destination.SetLength(0);
            destination.Position = 0;
        }

        return new AccountPictureResult(AccountPictureStatus.NotFound);
    }

    public async Task<AccountPictureResult> RemoveAsync(Guid actorId, Guid ownerId, Guid pictureId,
        CancellationToken cancellationToken)
    {
        var actor = await profiles.GetProfileAsync(actorId, cancellationToken);

        if (actor is null || (actorId != ownerId && actor.Role != AccountRoles.Administrator))
        {
            return new AccountPictureResult(AccountPictureStatus.Forbidden);
        }

        await using var transfer = await locks.AcquireAsync(ownerId, cancellationToken);
        return Result(await pictures.RemoveAsync(ownerId, pictureId, actorId, cancellationToken));
    }

    private static AccountPictureResult Result(AccountPicturePreparation result)
    {
        var picture = result.Picture;
        return new AccountPictureResult(result.Status,
            picture is null ? null : Receipt(picture, result.Status, result.Removal));
    }

    private static AccountPictureReceipt Receipt(AccountPicture picture)
    {
        return Receipt(picture, AccountPictureStatus.Available, null);
    }

    private static AccountPictureReceipt Receipt(AccountPicture picture, AccountPictureStatus status,
        AccountPictureRemoval? removal)
    {
        return new AccountPictureReceipt(picture.Id, picture.ContentType, picture.Bytes, picture.CreatedAt,
            picture.ExpiresAt, picture.AvailableAt, status.ToString(), removal?.Reason.ToString(),
            removal?.RequestedAt, removal?.DeletedAt);
    }
}
