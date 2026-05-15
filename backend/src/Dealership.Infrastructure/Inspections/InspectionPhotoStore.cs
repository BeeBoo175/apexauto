using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Dealership.Infrastructure.Photos;

namespace Dealership.Infrastructure.Inspections;

public sealed class InspectionPhotoStore(DealershipDbContext database, IInspectionAssignmentStore assignments, PhotoCleanupSignal cleanup)
    : IInspectionPhotoStore
{
    public async Task<InspectionPhotoStatus> CheckUploadAsync(Guid actorId, Guid inspectionId, long assignmentVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        return (await LockUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken)).Status;
    }

    public async Task<InspectionPhotoPreparation> PrepareAsync(Guid actorId, Guid inspectionId, Guid id,
        long assignmentVersion, string pointCode, PhotoContent content, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var (status, draft) = await LockUploadAsync(actorId, inspectionId, assignmentVersion, cancellationToken);
            if (status != InspectionPhotoStatus.Available)
            {
                return new(status);
            }

            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"inspection-photo:" + id}, 0))", cancellationToken);
            var existing = await database.InspectionPhotos.AsNoTracking()
                .SingleOrDefaultAsync(photo => photo.Id == id, cancellationToken);
            var now = await DatabaseTimeAsync(cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(inspectionId, actorId, assignmentVersion, pointCode, content)
                    ? await ClassifyAsync(existing, now, cancellationToken) : new(InspectionPhotoStatus.Conflict);
            }

            var photo = new InspectionPhoto(id, inspectionId, draft!.BranchId, actorId, assignmentVersion,
                pointCode, content, now, lifetime);
            database.InspectionPhotos.Add(photo);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            cleanup.Request();
            return new(InspectionPhotoStatus.Pending, photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionPhotoPreparation> ConfirmAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var (status, draft) = await LockUploadAsync(actorId, inspectionId, null, cancellationToken);
            if (status != InspectionPhotoStatus.Available)
            {
                return new(status);
            }

            var photo = await database.InspectionPhotos.FromSqlInterpolated(
                $"SELECT * FROM \"InspectionPhotos\" WHERE \"Id\" = {id} AND \"InspectionId\" = {inspectionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (photo is null)
            {
                return new(InspectionPhotoStatus.NotFound);
            }

            if (photo.ActorId != actorId || photo.AssignmentVersion != draft!.AssignmentVersion)
            {
                return new(InspectionPhotoStatus.Conflict);
            }

            var now = await DatabaseTimeAsync(cancellationToken);
            var result = await ClassifyAsync(photo, now, cancellationToken);
            if (result.Status != InspectionPhotoStatus.Pending)
            {
                return result;
            }

            photo.ConfirmAvailable(now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            cleanup.Request();
            return new(InspectionPhotoStatus.Available, photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<InspectionPhotoPreparation> FindAsync(Guid actorId, Guid inspectionId, Guid id,
        CancellationToken cancellationToken)
    {
        var access = await assignments.ReadAsync(actorId, inspectionId, cancellationToken);
        if (access.Status != InspectionAssignmentStatus.Succeeded)
        {
            return new(access.Status == InspectionAssignmentStatus.Forbidden
                ? InspectionPhotoStatus.Forbidden : InspectionPhotoStatus.NotFound);
        }

        var photo = await database.InspectionPhotos.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.InspectionId == inspectionId, cancellationToken);
        return photo is null ? new(InspectionPhotoStatus.NotFound)
            : await ClassifyAsync(photo, await DatabaseTimeAsync(cancellationToken), cancellationToken);
    }

    private async Task<InspectionPhotoPreparation> ClassifyAsync(InspectionPhoto photo, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var removal = await database.InspectionPhotoRemovals.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PhotoId == photo.Id, cancellationToken);
        if (removal is not null)
        {
            return new(removal.DeletedAt is null ? InspectionPhotoStatus.DeletionPending : InspectionPhotoStatus.Deleted, photo)
            {
                RemovalRequestedAt = removal.RequestedAt,
                DeletedAt = removal.DeletedAt
            };
        }

        if (now >= photo.ExpiresAt &&
            !await database.InspectionDraftPhotos.AnyAsync(link => link.PhotoId == photo.Id, cancellationToken))
        {
            return new(InspectionPhotoStatus.Gone, photo);
        }

        return new(photo.AvailableAt is null ? InspectionPhotoStatus.Pending : InspectionPhotoStatus.Available, photo);
    }

    private async Task<(InspectionPhotoStatus Status, InspectionDraft? Draft)> LockUploadAsync(Guid actorId,
        Guid inspectionId, long? assignmentVersion, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true, BranchId: not null, Role: AccountRoles.WorkshopTechnician } ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken))
        {
            return (InspectionPhotoStatus.Forbidden, null);
        }

        var vehicleId = await database.InspectionDrafts.Where(draft => draft.Id == inspectionId)
            .Select(draft => (Guid?)draft.VehicleId).SingleOrDefaultAsync(cancellationToken);
        var vehicle = await database.Vehicles.FromSqlInterpolated(
            $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var draft = await database.InspectionDrafts.FromSqlInterpolated(
            $"SELECT * FROM \"InspectionDrafts\" WHERE \"Id\" = {inspectionId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (vehicle is null || draft is null || draft.BranchId != actor.BranchId || draft.TechnicianId != actorId)
        {
            return (InspectionPhotoStatus.NotFound, null);
        }

        if (vehicle.Status != VehicleStatus.PendingInspection || draft.IsSigned || assignmentVersion is < 1 ||
            assignmentVersion.HasValue && draft.AssignmentVersion != assignmentVersion)
        {
            return (InspectionPhotoStatus.Conflict, null);
        }

        return (InspectionPhotoStatus.Available, draft);
    }

    private Task<DateTimeOffset> DatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.DatabaseNowAsync(cancellationToken);
    }
}
