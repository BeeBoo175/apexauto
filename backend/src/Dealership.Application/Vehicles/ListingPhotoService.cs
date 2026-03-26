using Dealership.Application.Photos;
using Dealership.Domain.Photos;

namespace Dealership.Application.Vehicles;

public sealed class ListingPhotoService(IListingPhotoStore photos, IPhotoImageValidator validator, PhotoTransferService transfers)
{
    public Task<ListingPhotoStatus> CheckUploadAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken)
    {
        return photos.CheckUploadAsync(actorId, vehicleId, cancellationToken);
    }

    public async Task<ListingPhotoResult> UploadAsync(Guid actorId, Guid vehicleId, Guid id, string contentType,
        Stream content, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !PhotoContent.IsSupportedContentType(contentType))
        {
            return new(ListingPhotoStatus.Invalid);
        }

        var access = await photos.CheckUploadAsync(actorId, vehicleId, cancellationToken);
        if (access != ListingPhotoStatus.Available)
        {
            return new(access);
        }

        var validated = await validator.ValidateAsync(content, contentType, cancellationToken);
        if (validated is null)
        {
            return new(ListingPhotoStatus.Invalid);
        }

        return Result(await transfers.UploadAsync(id, content,
            token => photos.PrepareAsync(actorId, vehicleId, id, validated, token),
            prepared => prepared.Status == ListingPhotoStatus.Pending ? prepared.Photo : null,
            token => photos.ConfirmAsync(actorId, vehicleId, id, token), cancellationToken));
    }

    public async Task<ListingPhotoResult> ReceiptAsync(Guid actorId, Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        return Result(await photos.FindAsync(actorId, vehicleId, id, cancellationToken));
    }

    public async Task<ListingPhotoResult> DownloadAsync(Guid? actorId, Guid vehicleId, Guid id,
        Stream destination, CancellationToken cancellationToken)
    {
        return Result(await transfers.DownloadAsync(id, destination,
            token => photos.FindAsync(actorId, vehicleId, id, token),
            found => found.Status == ListingPhotoStatus.Available ? found.Photo : null, cancellationToken));
    }

    private static ListingPhotoResult Result(ListingPhotoPreparation result)
    {
        var photo = result.Photo;
        return new(result.Status, photo is null ? null : new(photo.Id, photo.VehicleId, photo.ContentType,
            photo.Bytes, photo.Width, photo.Height, photo.CreatedAt, photo.AvailableAt));
    }
}
