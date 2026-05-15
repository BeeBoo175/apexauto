using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public enum ListingPhotoStatus
{
    Available,
    Pending,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record ListingPhotoReceipt(Guid Id, Guid VehicleId, string ContentType, long Bytes,
    int Width, int Height, DateTimeOffset CreatedAt, DateTimeOffset? AvailableAt);

public sealed record ListingPhotoResult(ListingPhotoStatus Status, ListingPhotoReceipt? Receipt = null)
{
    public static ListingPhotoResult From(ListingPhotoPreparation result)
    {
        var photo = result.Photo;
        return new(result.Status, photo is null ? null : new(photo.Id, photo.VehicleId, photo.ContentType,
            photo.Bytes, photo.Width, photo.Height, photo.CreatedAt, photo.AvailableAt));
    }
}

public sealed record ListingPhotoPreparation(ListingPhotoStatus Status, ListingPhoto? Photo = null);

public interface IListingPhotoStore
{
    Task<ListingPhotoStatus> CheckUploadAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken);

    Task<ListingPhotoPreparation> PrepareAsync(Guid actorId, Guid vehicleId, Guid id,
        PhotoContent content, CancellationToken cancellationToken);

    Task<ListingPhotoPreparation> ConfirmAsync(Guid actorId, Guid vehicleId, Guid id, CancellationToken cancellationToken);

    Task<ListingPhotoPreparation> FindAsync(Guid? actorId, Guid vehicleId, Guid id, CancellationToken cancellationToken);

    Task<ListingPhotoPreparation> FindReservationAsync(Guid actorId, Guid reservationId, Guid id,
        CancellationToken cancellationToken);
}
