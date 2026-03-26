using Dealership.Domain.Photos;

namespace Dealership.Domain.Vehicles;

public sealed class ListingPhoto : IPhotoObject
{
    private ListingPhoto()
    {
    }

    public ListingPhoto(Guid id, Guid vehicleId, Guid branchId, Guid actorId, PhotoContent content, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || vehicleId == Guid.Empty || branchId == Guid.Empty || actorId == Guid.Empty || !content.IsValid)
        {
            throw new ArgumentException("Listing photography requires valid ownership and image content.");
        }

        Id = id;
        VehicleId = vehicleId;
        BranchId = branchId;
        ActorId = actorId;
        ObjectKey = $"listings/photos/{id:N}";
        ContentType = content.ContentType;
        Bytes = content.Bytes;
        Sha256 = content.Sha256;
        Width = content.Width;
        Height = content.Height;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Bytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AvailableAt { get; private set; }

    public bool Matches(Guid vehicleId, Guid actorId, PhotoContent content) =>
        VehicleId == vehicleId && ActorId == actorId && ContentType == content.ContentType && Bytes == content.Bytes &&
        Sha256 == content.Sha256 && Width == content.Width && Height == content.Height;

    public void ConfirmAvailable(DateTimeOffset now)
    {
        if (now < CreatedAt)
        {
            throw new InvalidOperationException("An upload cannot be confirmed before creation.");
        }

        AvailableAt ??= now;
    }
}

public sealed record ListingRevisionPhoto(Guid RevisionId, Guid PhotoId, Guid VehicleId, Guid BranchId, int Position);
