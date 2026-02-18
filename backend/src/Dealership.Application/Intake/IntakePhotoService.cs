using Dealership.Domain.Photos;
using Dealership.Application.Photos;
using Dealership.Application.Accounts;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed class IntakePhotoService(ICustomerAccountQuery accounts, IPhotoImageValidator validator,
    IIntakePhotoStore photos, IPhotoStorage storage, IntakePhotoPolicy policy, IPhotoTransferLock transfers)
{
    public async Task<bool> CanUploadAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        return (await accounts.GetAccountAsync(ownerId, cancellationToken)) is { EmailVerified: true };
    }

    public async Task<PhotoResult> UploadAsync(Guid ownerId, Guid id, PhotoAngle angle, string contentType,
        Stream content, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !Enum.IsDefined(angle) || !PhotoContent.IsSupportedContentType(contentType))
        {
            return new PhotoResult(PhotoStatus.Invalid);
        }

        if (!await CanUploadAsync(ownerId, cancellationToken))
        {
            return new PhotoResult(PhotoStatus.Forbidden);
        }

        var validated = await validator.ValidateAsync(content, contentType, cancellationToken);

        if (validated is null)
        {
            return new PhotoResult(PhotoStatus.Invalid);
        }

        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);

        var prepared = await photos.PrepareAsync(ownerId, id, angle, validated, policy.UnsubmittedLifetime,
            cancellationToken);

        if (prepared.Status != PhotoStatus.Pending)
        {
            return Result(prepared);
        }

        content.Position = 0;
        await storage.PutAsync(prepared.Photo!, content, cancellationToken);
        return Result(await photos.ConfirmAsync(ownerId, id, cancellationToken));
    }

    public async Task<PhotoResult> GetReceiptAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        return Result(await photos.FindAsync(ownerId, id, cancellationToken));
    }

    public async Task<PhotoResult> DownloadAsync(Guid ownerId, Guid id, Stream destination,
        CancellationToken cancellationToken)
    {
        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);

        var found = await photos.FindAsync(ownerId, id, cancellationToken);

        if (found.Status != PhotoStatus.Available)
        {
            return Result(found);
        }

        await storage.CopyToAsync(found.Photo!, destination, cancellationToken);
        // Recheck eligibility and expiry after provider I/O, before releasing the bytes.
        return Result(await photos.FindAsync(ownerId, id, cancellationToken));
    }

    private static PhotoResult Result(PhotoPreparation result)
    {
        var photo = result.Photo;
        var receipt = photo is null ? null : new PhotoReceipt(photo.Id, photo.Angle.ToString(), photo.ContentType,
            photo.Bytes, photo.Width, photo.Height, photo.CreatedAt, photo.ExpiresAt, photo.AvailableAt,
            result.Status.ToString(), result.Removal?.Reason.ToString(), result.Removal?.RequestedAt, result.Removal?.DeletedAt);
        return new PhotoResult(result.Status, receipt);
    }
}
