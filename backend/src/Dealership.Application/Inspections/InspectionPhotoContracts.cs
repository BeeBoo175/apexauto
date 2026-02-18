using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;

namespace Dealership.Application.Inspections;

public enum InspectionPhotoStatus
{
    Available,
    Pending,
    Invalid,
    Forbidden,
    Conflict,
    NotFound,
    Gone,
    DeletionPending,
    Deleted
}

public sealed record InspectionPhotoReceipt(Guid Id, Guid InspectionId, Guid ActorId, long AssignmentVersion,
    string PointCode, string ContentType, long Bytes, int Width, int Height, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? AvailableAt)
{
    public DateTimeOffset? RemovalRequestedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
}

public sealed record InspectionPhotoResult(InspectionPhotoStatus Status, InspectionPhotoReceipt? Receipt = null);

public sealed record InspectionPhotoPreparation(InspectionPhotoStatus Status, InspectionPhoto? Photo = null)
{
    public DateTimeOffset? RemovalRequestedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
}

public sealed record InspectionPhotoPolicy(TimeSpan UnattachedLifetime);

public interface IInspectionPhotoStore
{
    Task<InspectionPhotoStatus> CheckUploadAsync(Guid actorId, Guid inspectionId, long assignmentVersion,
        CancellationToken cancellationToken);

    Task<InspectionPhotoPreparation> PrepareAsync(Guid actorId, Guid inspectionId, Guid id, long assignmentVersion,
        string pointCode, PhotoContent content, TimeSpan lifetime, CancellationToken cancellationToken);

    Task<InspectionPhotoPreparation> ConfirmAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken);

    Task<InspectionPhotoPreparation> FindAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken);
}
