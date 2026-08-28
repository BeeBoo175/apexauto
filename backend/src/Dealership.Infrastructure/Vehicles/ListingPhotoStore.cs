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
            var now = await database.DatabaseNowAsync(cancellationToken);
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

            photo.ConfirmAvailable(await database.DatabaseNowAsync(cancellationToken));
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
        if (!actorId.HasValue)
        {
            return await ReservationReadSnapshot.ReadAsync(database,
                () => FindPublicAsync(vehicleId, id, cancellationToken),
                new ListingPhotoPreparation(ListingPhotoStatus.Unavailable), cancellationToken);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var access = await LockAccessAsync(actorId.Value, vehicleId, cancellationToken);
        if (access != ListingPhotoStatus.Available)
        {
            return new(access);
        }

        return await FindPhotoAsync(vehicleId, id, cancellationToken);
    }

    public async Task<ListingPhotoPreparation> FindReservationAsync(Guid actorId, Guid reservationId, Guid id,
        CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == actorId, cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.Customer })
        {
            return new(ListingPhotoStatus.Forbidden);
        }

        // The retained approved revision grants access, never the current draft or private evidence.
        var vehicleId = await (from reservation in database.Reservations
                               join publication in database.ListingPublications on reservation.PublicationId equals publication.Id
                               join photo in database.ListingRevisionPhotos on publication.RevisionId equals photo.RevisionId
                               where reservation.Id == reservationId && reservation.CustomerId == actorId && photo.PhotoId == id
                               select (Guid?)reservation.VehicleId).SingleOrDefaultAsync(cancellationToken);
        return vehicleId.HasValue
            ? await FindPhotoAsync(vehicleId.Value, id, cancellationToken)
            : new(ListingPhotoStatus.NotFound);
    }

    private async Task<ListingPhotoPreparation> FindPublicAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
        await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var isPublic = await (from listing in PublicListingQuery.Read(database, now)
                              join link in database.ListingRevisionPhotos on listing.Revision.Id equals link.RevisionId
                              where link.PhotoId == id && listing.Vehicle.Id == vehicleId
                              select link.PhotoId).AnyAsync(cancellationToken);
        return isPublic ? await FindPhotoAsync(vehicleId, id, cancellationToken) : new(ListingPhotoStatus.NotFound);
    }

    private async Task<ListingPhotoPreparation> FindPhotoAsync(Guid vehicleId, Guid id, CancellationToken cancellationToken)
    {
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
}
