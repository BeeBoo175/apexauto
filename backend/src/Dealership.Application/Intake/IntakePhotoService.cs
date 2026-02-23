using Dealership.Domain.Photos;
using Dealership.Application.Photos;
using Dealership.Application.Accounts;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed class IntakePhotoService(ICustomerAccountQuery accounts, IPhotoImageValidator validator,
    IIntakePhotoStore photos, IntakePhotoPolicy policy, PhotoTransferService transfers)
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

        return Result(await transfers.UploadAsync(id, content,
            token => photos.PrepareAsync(ownerId, id, angle, validated, policy.UnsubmittedLifetime, token),
            prepared => prepared.Status == PhotoStatus.Pending ? prepared.Photo : null,
            token => photos.ConfirmAsync(ownerId, id, token), cancellationToken));
    }

    public async Task<PhotoResult> GetReceiptAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        return Result(await photos.FindAsync(ownerId, id, cancellationToken));
    }

    public async Task<PhotoResult> DownloadAsync(Guid ownerId, Guid id, Stream destination,
        CancellationToken cancellationToken)
    {
        return Result(await transfers.DownloadAsync(id, destination,
            token => photos.FindAsync(ownerId, id, token),
            found => found.Status == PhotoStatus.Available ? found.Photo : null, cancellationToken));
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
