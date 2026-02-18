using Dealership.Domain.Photos;

namespace Dealership.Domain.Intake;

public enum PhotoAngle
{
    Driver,
    Passenger,
    Front,
    Rear
}

public sealed class IntakePhoto : IPhotoObject
{
    private IntakePhoto()
    {
    }

    public IntakePhoto(Guid id, Guid ownerId, PhotoAngle angle, PhotoContent content,
        DateTimeOffset createdAt, TimeSpan lifetime)
    {
        if (id == Guid.Empty || ownerId == Guid.Empty || !Enum.IsDefined(angle) || lifetime <= TimeSpan.Zero ||
            !content.IsValid)
        {
            throw new ArgumentException("Photo identity, content and lifetime must be valid.");
        }

        Id = id;
        OwnerId = ownerId;
        Angle = angle;
        ContentType = content.ContentType;
        Bytes = content.Bytes;
        Sha256 = content.Sha256;
        Width = content.Width;
        Height = content.Height;
        ObjectKey = $"intake/photos/{id:N}";
        CreatedAt = createdAt;
        ExpiresAt = createdAt + lifetime;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public PhotoAngle Angle { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Bytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AvailableAt { get; private set; }

    public bool Matches(Guid ownerId, PhotoAngle angle, PhotoContent content)
    {
        return OwnerId == ownerId && Angle == angle && ContentType == content.ContentType &&
            Bytes == content.Bytes && Sha256 == content.Sha256 && Width == content.Width && Height == content.Height;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void ConfirmAvailable(DateTimeOffset now)
    {
        if (IsExpired(now) || now < CreatedAt)
        {
            throw new InvalidOperationException("Expired or future photos cannot become available.");
        }

        AvailableAt ??= now;
    }
}
