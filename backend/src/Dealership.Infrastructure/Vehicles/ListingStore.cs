using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingStore(DealershipDbContext database) : IListingStore
{
    public async Task<IListingTransaction> LockAsync(Guid actorId, Guid vehicleId, Guid operationId,
        CancellationToken cancellationToken, IReadOnlyList<Guid>? photoIds = null)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"listing:" + operationId}, 0))", cancellationToken);
            var actor = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var manager = actor is null ? null : new ListingManager(actor.IsActive, actor.Role, actor.BranchId);
            if (actor is not { IsActive: true } ||
                actor.Role is not (AccountRoles.BranchManager or AccountRoles.GeneralManager) ||
                actor.Role == AccountRoles.BranchManager && actor.BranchId is null)
            {
                return new ListingTransaction(database, transaction,
                    new(manager, false, null, null, null, false, null, default, null, null, null, []));
            }

            var branchId = await database.Vehicles.Where(item => item.Id == vehicleId)
                .Select(item => (Guid?)item.BranchId).SingleOrDefaultAsync(cancellationToken);
            var branchActive = await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {branchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken);
            var vehicle = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var latestRevision = await database.ListingRevisions.AsNoTracking()
                .Where(item => item.VehicleId == vehicleId).OrderByDescending(item => item.Version)
                .FirstOrDefaultAsync(cancellationToken);
            var latestPublication = await (from publication in database.ListingPublications.AsNoTracking()
                                           join revision in database.ListingRevisions on publication.RevisionId equals revision.Id
                                           where publication.VehicleId == vehicleId
                                           orderby revision.Version descending
                                           select publication).FirstOrDefaultAsync(cancellationToken);
            var isWithdrawn = latestPublication is not null && await database.ListingWithdrawals
                .AnyAsync(item => item.PublicationId == latestPublication.Id, cancellationToken);
            var signedId = await (from draft in database.InspectionDrafts
                                  join signed in database.InspectionSignedRevisions on draft.Id equals signed.InspectionId
                                  where draft.VehicleId == vehicleId && draft.IsSigned && signed.Revision == draft.Revision
                                  select (Guid?)signed.Id).SingleOrDefaultAsync(cancellationToken);
            var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
                .SingleAsync(cancellationToken);
            var existingDraft = await database.ListingRevisions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var existingPublication = await database.ListingPublications.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var existingWithdrawal = await database.ListingWithdrawals.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            var requestedPhotos = photoIds?.ToArray() ?? [];
            var availablePhotoIds = await database.ListingPhotos
                .Where(item => item.VehicleId == vehicleId && item.AvailableAt != null && requestedPhotos.Contains(item.Id))
                .Select(item => item.Id).ToArrayAsync(cancellationToken);
            return new ListingTransaction(database, transaction, new(manager, branchActive, vehicle,
                latestRevision, latestPublication, isWithdrawn, signedId, now,
                existingDraft, existingPublication, existingWithdrawal, availablePhotoIds));
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}
