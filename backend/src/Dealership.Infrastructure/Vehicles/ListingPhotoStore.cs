using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingPhotoStore(DealershipDbContext database) : IListingPhotoStore
{
    public async Task<ListingPhotoStatus> CheckUploadAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        return await LockAccessAsync(actorId, vehicleId, cancellationToken);
    }

    public async Task<ListingPhotoPreparation> PrepareAsync(Guid actorId, Guid vehicleId, Guid id,
        PhotoContent content, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"listing-photo:" + id}, 0))", cancellationToken);
            var access = await LockAccessAsync(actorId, vehicleId, cancellationToken);
            if (access != ListingPhotoStatus.Available)
            {
                return new(access);
            }

            var existing = await database.ListingPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (existing is not null)
            {
                return existing.Matches(vehicleId, actorId, content) ? Classify(existing) : new(ListingPhotoStatus.Conflict);
            }

            var branchId = await database.Vehicles.Where(item => item.Id == vehicleId).Select(item => item.BranchId).SingleAsync(cancellationToken);
            var now = await DatabaseTimeAsync(cancellationToken);
            var photo = new ListingPhoto(id, vehicleId, branchId, actorId, content, now);
            database.ListingPhotos.Add(photo);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ListingPhotoStatus.Pending, photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<ListingPhotoPreparation> ConfirmAsync(Guid actorId, Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"listing-photo:" + id}, 0))", cancellationToken);
            var access = await LockAccessAsync(actorId, vehicleId, cancellationToken);
            if (access != ListingPhotoStatus.Available)
            {
                return new(access);
            }

            var photo = await database.ListingPhotos.SingleOrDefaultAsync(item => item.Id == id && item.VehicleId == vehicleId, cancellationToken);
            if (photo is null)
            {
                return new(ListingPhotoStatus.NotFound);
            }

            if (photo.ActorId != actorId)
            {
                return new(ListingPhotoStatus.Conflict);
            }

            photo.ConfirmAvailable(await DatabaseTimeAsync(cancellationToken));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Classify(photo);
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    public async Task<ListingPhotoPreparation> FindAsync(Guid? actorId, Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (actorId.HasValue)
        {
            var access = await LockAccessAsync(actorId.Value, vehicleId, cancellationToken);
            if (access != ListingPhotoStatus.Available)
            {
                return new(access);
            }
        }
        else
        {
            var isPublic = await (from link in database.ListingRevisionPhotos
                                  join publication in database.ListingPublications on link.RevisionId equals publication.RevisionId
                                  join revision in database.ListingRevisions on link.RevisionId equals revision.Id
                                  join vehicle in database.Vehicles on publication.VehicleId equals vehicle.Id
                                  join branch in database.Branches on vehicle.BranchId equals branch.Id
                                  where link.PhotoId == id && vehicle.Id == vehicleId && branch.IsActive &&
                                      vehicle.Status == VehicleStatus.Available &&
                                      !database.ListingWithdrawals.Any(item => item.PublicationId == publication.Id) &&
                                      !database.ListingPublications.Any(later => later.VehicleId == vehicleId &&
                                          database.ListingRevisions.Any(r => r.Id == later.RevisionId && r.Version > revision.Version)) &&
                                      database.InspectionSignedRevisions.Any(signed => signed.Id == publication.SignedRevisionId &&
                                          database.InspectionDrafts.Any(draft => draft.VehicleId == vehicleId && draft.Id == signed.InspectionId &&
                                              draft.IsSigned && draft.Revision == signed.Revision))
                                  select link.PhotoId).AnyAsync(cancellationToken);
            if (!isPublic)
            {
                return new(ListingPhotoStatus.NotFound);
            }
        }

        var photo = await database.ListingPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.VehicleId == vehicleId, cancellationToken);
        return photo is null ? new(ListingPhotoStatus.NotFound) : Classify(photo);
    }

    private async Task<ListingPhotoStatus> LockAccessAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var actor = await database.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } || actor.Role is not (AccountRoles.BranchManager or AccountRoles.GeneralManager) ||
            actor.Role == AccountRoles.BranchManager && actor.BranchId is null)
        {
            return ListingPhotoStatus.Forbidden;
        }

        var branchId = await database.Vehicles.Where(item => item.Id == vehicleId).Select(item => (Guid?)item.BranchId).SingleOrDefaultAsync(cancellationToken);
        var branchActive = await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {branchId} FOR SHARE")
            .AnyAsync(item => item.IsActive, cancellationToken);
        var exists = await database.Vehicles.FromSqlInterpolated($"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
            .AnyAsync(cancellationToken);
        return exists && branchActive && (actor.Role == AccountRoles.GeneralManager || actor.BranchId == branchId)
            ? ListingPhotoStatus.Available : ListingPhotoStatus.NotFound;
    }

    private static ListingPhotoPreparation Classify(ListingPhoto photo) =>
        new(photo.AvailableAt is null ? ListingPhotoStatus.Pending : ListingPhotoStatus.Available, photo);

    private Task<DateTimeOffset> DatabaseTimeAsync(CancellationToken cancellationToken)
    {
        return database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
    }
}
