using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public enum PhotoStatus
{
    Available,
    Pending,
    Invalid,
    Forbidden,
    Conflict,
    NotFound
}

public sealed record PhotoReceipt(Guid Id, string Angle, string ContentType, long Bytes, int Width, int Height,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AvailableAt);

public sealed record PhotoResult(PhotoStatus Status, PhotoReceipt? Receipt = null);

public sealed record PhotoPreparation(PhotoStatus Status, IntakePhoto? Photo = null);

public sealed record IntakePhotoPolicy(TimeSpan UnsubmittedLifetime);

public interface IPhotoImageValidator
{
    Task<PhotoContent?> ValidateAsync(Stream content, string contentType, CancellationToken cancellationToken);
}

public interface IIntakePhotoStore
{
    Task<PhotoPreparation> PrepareAsync(Guid ownerId, Guid id, PhotoAngle angle, PhotoContent content,
        TimeSpan lifetime, CancellationToken cancellationToken);

    Task<PhotoPreparation> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    Task<PhotoPreparation> ConfirmAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
}

public interface IPhotoStorage
{
    Task PutAsync(IntakePhoto photo, Stream content, CancellationToken cancellationToken);

    Task CopyToAsync(IntakePhoto photo, Stream destination, CancellationToken cancellationToken);
}

public sealed class PhotoStorageException : Exception
{
    public PhotoStorageException(Exception? innerException = null) : base("Photo storage is temporarily unavailable.", innerException)
    {
    }
}
