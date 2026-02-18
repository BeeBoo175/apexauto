using Dealership.Domain.Intake;
using Dealership.Domain.Photos;

namespace Dealership.Application.Intake;

public enum PhotoStatus
{
    Available,
    Pending,
    DeletionPending,
    Deleted,
    Invalid,
    Forbidden,
    Conflict,
    NotFound
}

public sealed record PhotoReceipt(Guid Id, string Angle, string ContentType, long Bytes, int Width, int Height,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AvailableAt,
    string Status, string? RemovalReason, DateTimeOffset? RemovalRequestedAt, DateTimeOffset? DeletedAt);

public sealed record PhotoResult(PhotoStatus Status, PhotoReceipt? Receipt = null);

public sealed record PhotoPreparation(PhotoStatus Status, IntakePhoto? Photo = null, PhotoRemoval? Removal = null);

public sealed record IntakePhotoPolicy(TimeSpan UnsubmittedLifetime);

public interface IIntakePhotoStore
{
    Task<PhotoPreparation> PrepareAsync(Guid ownerId, Guid id, PhotoAngle angle, PhotoContent content,
        TimeSpan lifetime, CancellationToken cancellationToken);

    Task<PhotoPreparation> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    Task<PhotoPreparation> ConfirmAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
}
