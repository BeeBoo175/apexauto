using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;

namespace Dealership.Application.Inspections;

public sealed class InspectionPhotoService(IInspectionPhotoStore photos, IPhotoImageValidator validator,
    IPhotoStorage storage, IPhotoTransferLock transfers, InspectionPhotoPolicy policy)
{
    public Task<InspectionPhotoStatus> CheckUploadAsync(Guid actorId, Guid inspectionId, long assignmentVersion,
        CancellationToken cancellationToken)
    {
        return photos.CheckUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken);
    }

    public async Task<InspectionPhotoResult> UploadAsync(Guid actorId, Guid inspectionId, Guid id,
        long assignmentVersion, string pointCode, string contentType, Stream content, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !PhotoContent.IsSupportedContentType(contentType) ||
            !InspectionCatalog.Points.Any(point => point.Code == pointCode))
        {
            return new(InspectionPhotoStatus.Invalid);
        }

        var access = await CheckUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken);
        if (access != InspectionPhotoStatus.Available)
        {
            return new(access);
        }

        var validated = await validator.ValidateAsync(content, contentType, cancellationToken);
        if (validated is null)
        {
            return new(InspectionPhotoStatus.Invalid);
        }

        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);
        var prepared = await photos.PrepareAsync(actorId, inspectionId, id, assignmentVersion, pointCode, validated,
            policy.UnattachedLifetime, cancellationToken);
        if (prepared.Status != InspectionPhotoStatus.Pending)
        {
            return Result(prepared);
        }

        content.Position = 0;
        await storage.PutAsync(prepared.Photo!, content, cancellationToken);
        return Result(await photos.ConfirmAsync(actorId, inspectionId, id, cancellationToken));
    }

    public async Task<InspectionPhotoResult> GetReceiptAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken)
    {
        return Result(await photos.FindAsync(actorId, inspectionId, id, cancellationToken));
    }

    public async Task<InspectionPhotoResult> DownloadAsync(Guid actorId, Guid inspectionId, Guid id,
        Stream destination, CancellationToken cancellationToken)
    {
        await using var transfer = await transfers.AcquireAsync(id, cancellationToken);
        var found = await photos.FindAsync(actorId, inspectionId, id, cancellationToken);
        if (found.Status != InspectionPhotoStatus.Available)
        {
            return Result(found);
        }

        await storage.CopyToAsync(found.Photo!, destination, cancellationToken);
        // Handover or role changes during provider I/O must prevent releasing private bytes.
        return Result(await photos.FindAsync(actorId, inspectionId, id, cancellationToken));
    }

    private static InspectionPhotoResult Result(InspectionPhotoPreparation result)
    {
        var photo = result.Photo;
        var receipt = photo is null ? null : new InspectionPhotoReceipt(photo.Id, photo.InspectionId, photo.ActorId,
            photo.AssignmentVersion, photo.PointCode, photo.ContentType, photo.Bytes, photo.Width, photo.Height,
            photo.CreatedAt, photo.ExpiresAt, photo.AvailableAt)
        {
            RemovalRequestedAt = result.RemovalRequestedAt,
            DeletedAt = result.DeletedAt
        };
        return new(result.Status, receipt);
    }
}
