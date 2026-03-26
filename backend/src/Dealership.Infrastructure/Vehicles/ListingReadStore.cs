using System.Data;
using System.Linq.Expressions;
using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingReadStore(DealershipDbContext database) : IListingReadStore
{
    private static readonly Expression<Func<Row, ListingManagerItem>> Item = row => new(
        row.Vehicle.Id, row.Vehicle.BranchId, row.BranchName, row.Vehicle.RegistrationPlate,
        row.Vehicle.Make, row.Vehicle.Model, row.Vehicle.Trim, row.Vehicle.ManufactureYear,
        row.Vehicle.MileageKilometres, row.Vehicle.Status, row.Draft == null ? null : row.Draft.Id,
        row.Draft == null ? 0 : row.Draft.Version, row.Draft == null ? null : row.Draft.VehiclePrice,
        row.Publication == null ? null : row.Publication.Id, row.Publication == null ? null : row.Publication.RevisionId,
        row.IsWithdrawn, row.IsPublic);

    public async Task<ListingManagerPageResult> ReadAsync(Guid actorId, ListingReadFilter filter,
        CancellationToken cancellationToken)
    {
        if (!filter.IsValid)
        {
            return new(ListingStatus.Invalid);
        }

        // Current manager scope, totals and editor summaries share one snapshot.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ReadActorAsync(actorId, filter.BranchId, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        var rows = Rows(actor.BranchId ?? filter.BranchId);
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search.Trim());
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Make, pattern, "!") || EF.Functions.ILike(row.Vehicle.Model, pattern, "!") ||
                EF.Functions.ILike(row.BranchName, pattern, "!"));
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderBy(row => row.BranchName).ThenBy(row => row.Vehicle.RegistrationPlate).ThenBy(row => row.Vehicle.Id)
            .Select(Item)
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize));
    }

    public async Task<ListingWorkspaceResult> WorkspaceAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ReadActorAsync(actorId, null, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        var vehicle = await Rows(actor.BranchId).Where(row => row.Vehicle.Id == vehicleId).Select(Item)
            .SingleOrDefaultAsync(cancellationToken);
        if (vehicle is null)
        {
            return new(ListingStatus.NotFound);
        }

        var draft = await database.ListingRevisions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.RevisionId, cancellationToken);
        var approved = await database.ListingRevisions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.PublishedRevisionId, cancellationToken);
        var publication = await database.ListingPublications.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.PublicationId, cancellationToken);
        var withdrawal = await database.ListingWithdrawals.AsNoTracking()
            .SingleOrDefaultAsync(row => row.PublicationId == vehicle.PublicationId, cancellationToken);
        var signedId = await (from signed in database.InspectionSignedRevisions.AsNoTracking()
                              join inspection in database.InspectionDrafts.AsNoTracking() on signed.InspectionId equals inspection.Id
                              where inspection.VehicleId == vehicleId && inspection.IsSigned && inspection.Revision == signed.Revision
                              select (Guid?)signed.Id).SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(vehicle, draft, approved, publication, withdrawal, signedId));
    }

    public async Task<ListingPhotoPageResult> PhotosAsync(Guid actorId, Guid vehicleId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || !Pagination.IsValid(page, pageSize))
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ReadActorAsync(actorId, null, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        if (!await database.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId &&
            (actor.BranchId == null || vehicle.BranchId == actor.BranchId) &&
            database.Branches.Any(branch => branch.Id == vehicle.BranchId && branch.IsActive), cancellationToken))
        {
            return new(ListingStatus.NotFound);
        }

        var photos = database.ListingPhotos.AsNoTracking().Where(row => row.VehicleId == vehicleId);
        var total = await photos.CountAsync(cancellationToken);
        var items = await photos.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id)
            .Select(row => new ListingPhotoItem(row.Id, row.ContentType, row.Bytes, row.Width, row.Height, row.CreatedAt, row.AvailableAt))
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, page, pageSize));
    }

    private IQueryable<Row> Rows(Guid? branchId)
    {
        var latest = database.ListingRevisions.AsNoTracking().Where(revision =>
            !database.ListingRevisions.Any(later => later.VehicleId == revision.VehicleId && later.Version > revision.Version));
        var publications = from publication in database.ListingPublications.AsNoTracking()
                           join revision in database.ListingRevisions on publication.RevisionId equals revision.Id
                           where !database.ListingPublications.Any(later => later.VehicleId == publication.VehicleId &&
                               database.ListingRevisions.Any(r => r.Id == later.RevisionId && r.Version > revision.Version))
                           select publication;
        var publicRows = PublicListingQuery.Read(database);
        return from vehicle in database.Vehicles.AsNoTracking()
               join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
               join saved in latest on vehicle.Id equals saved.VehicleId into drafts
               from draft in drafts.DefaultIfEmpty()
               join approved in publications on vehicle.Id equals approved.VehicleId into approvals
               from publication in approvals.DefaultIfEmpty()
               where branch.IsActive && (branchId == null || vehicle.BranchId == branchId)
               select new Row
               {
                   Vehicle = vehicle,
                   Draft = draft,
                   Publication = publication,
                   BranchName = branch.Name,
                   IsWithdrawn = publication != null && database.ListingWithdrawals.Any(row => row.PublicationId == publication.Id),
                   IsPublic = publicRows.Any(row => row.Vehicle.Id == vehicle.Id)
               };
    }

    private async Task<ListingManager?> ReadActorAsync(Guid actorId, Guid? selectedBranch, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().Where(user => user.Id == actorId)
            .Select(user => new ListingManager(user.IsActive, user.Role, user.BranchId)).SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true } || actor.Role is not (AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        if (actor.Role == AccountRoles.GeneralManager)
        {
            return actor;
        }

        if (actor.BranchId is null || selectedBranch is not null && selectedBranch != actor.BranchId ||
            !await database.Branches.AnyAsync(branch => branch.Id == actor.BranchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        return actor;
    }

    private sealed class Row
    {
        public required Vehicle Vehicle { get; init; }
        public required ListingRevision? Draft { get; init; }
        public required ListingPublication? Publication { get; init; }
        public required string BranchName { get; init; }
        public bool IsWithdrawn { get; init; }
        public bool IsPublic { get; init; }
    }
}
