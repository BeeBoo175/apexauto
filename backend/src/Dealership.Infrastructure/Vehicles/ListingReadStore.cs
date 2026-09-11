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
        row.IsWithdrawn, row.IsPublic, row.Approved == null ? null : row.Approved.VehiclePrice,
        row.Draft == null ? null : row.Draft.RecordedAt,
        row.Publication == null ? null : row.Publication.RecordedAt,
        row.Withdrawal == null ? null : row.Withdrawal.RecordedAt,
        row.IsReserved, row.HasCurrentPublicationInspection);

    public async Task<ListingManagerPageResult> ReadAsync(Guid actorId, ListingReadFilter filter,
        CancellationToken cancellationToken)
    {
        if (!filter.IsValid)
        {
            return new(ListingStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(actorId, filter, cancellationToken),
            new ListingManagerPageResult(ListingStatus.Unavailable), cancellationToken);
    }

    private async Task<ListingManagerPageResult> ReadSnapshotAsync(Guid actorId, ListingReadFilter filter,
        CancellationToken cancellationToken)
    {
        var actor = await ListingReadScope.ReadActorAsync(database, actorId, filter.BranchId, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var rows = Rows(actor.BranchId ?? filter.BranchId, now);
        if (filter.Registration.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Registration);
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!"));
        }
        if (filter.Vehicle.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Vehicle);
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.ManufactureYear + " " + row.Vehicle.Make + " " +
                row.Vehicle.Model + " " + row.Vehicle.Trim, pattern, "!"));
        }
        if (filter.ActivityFrom is not null)
        {
            rows = rows.Where(row => row.ActivityAt >= filter.ActivityFrom && row.ActivityAt < filter.ActivityUntil);
        }
        if (filter.MinimumPrice is not null)
        {
            rows = rows.Where(row => row.Draft != null && row.Draft.VehiclePrice >= filter.MinimumPrice);
        }
        if (filter.MaximumPrice is not null)
        {
            rows = rows.Where(row => row.Draft != null && row.Draft.VehiclePrice <= filter.MaximumPrice);
        }
        if (filter.MinimumPublishedPrice is not null)
        {
            rows = rows.Where(row => row.Approved != null && row.Approved.VehiclePrice >= filter.MinimumPublishedPrice);
        }
        if (filter.MaximumPublishedPrice is not null)
        {
            rows = rows.Where(row => row.Approved != null && row.Approved.VehiclePrice <= filter.MaximumPublishedPrice);
        }
        if (filter.Search.Length > 0)
        {
            var pattern = LiteralSearch.Pattern(filter.Search.Trim());
            // Registration, VIN and the vehicle as the table shows it (year included); branch names only for the group scope.
            var searchBranch = actor.BranchId is null;
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.Vin, pattern, "!") ||
                EF.Functions.ILike(row.Vehicle.ManufactureYear + " " + row.Vehicle.Make + " " + row.Vehicle.Model + " " +
                    row.Vehicle.Trim, pattern, "!") ||
                searchBranch && EF.Functions.ILike(row.BranchName, pattern, "!"));
        }

        if (filter.Inspection.Length > 0)
        {
            var status = Enum.Parse<VehicleStatus>(filter.Inspection);
            rows = rows.Where(row => row.Vehicle.Status == status);
        }

        // Publication filtering and ordering both depend on current hold availability.
        var publicationDependent = filter.Sort == "publication" || filter.Publication.Length > 0;
        if (publicationDependent && !await ReservationReadSnapshot.ReconcileDueAsync(database,
            rows.Select(row => row.Vehicle.Id), filter.PageSize, cancellationToken))
        {
            return new(ListingStatus.Unavailable);
        }

        rows = FilterPublication(rows, filter.Publication);
        var total = await rows.CountAsync(cancellationToken);
        var pageIds = await Order(rows, filter)
            .Select(row => row.Vehicle.Id)
            .ReadPageAsync(filter.Page, filter.PageSize, total, cancellationToken);
        var vehicleIds = await database.Vehicles.Where(vehicle => !publicationDependent && pageIds.Contains(vehicle.Id) &&
                database.Reservations.Any(item => item.VehicleId == vehicle.Id && item.IsActive))
            .OrderBy(vehicle => vehicle.Id).Select(vehicle => vehicle.Id).ToArrayAsync(cancellationToken);
        // Other sort keys do not depend on hold state; retain bounded page catch-up.
        foreach (var vehicleId in vehicleIds)
        {
            await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        }

        var items = await Order(rows.Where(row => pageIds.Contains(row.Vehicle.Id)), filter)
            .Select(Item).ToArrayAsync(cancellationToken);
        var counts = await CountsAsync(actor.BranchId ?? filter.BranchId, filter.PageSize, cancellationToken);
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, filter.Page, filter.PageSize, counts));
    }

    // Public state depends on holds, so due holds are reconciled first; without that the badges are left off.
    private async Task<ListingCounts?> CountsAsync(Guid? branchId, int batch, CancellationToken cancellationToken)
    {
        var now = await database.DatabaseNowAsync(cancellationToken);
        var scope = Rows(branchId, now);
        if (!await ReservationReadSnapshot.ReconcileDueAsync(database,
            scope.Select(row => row.Vehicle.Id), batch, cancellationToken))
        {
            return null;
        }

        return new ListingCounts(
            await scope.CountAsync(cancellationToken),
            await FilterPublication(scope, "unprepared").CountAsync(cancellationToken),
            await FilterPublication(scope, "draft").CountAsync(cancellationToken),
            await FilterPublication(scope, "published").CountAsync(cancellationToken),
            await FilterPublication(scope, "changes").CountAsync(cancellationToken),
            await FilterPublication(scope, "unavailable").CountAsync(cancellationToken),
            await FilterPublication(scope, "withdrawn").CountAsync(cancellationToken));
    }

    private static IOrderedQueryable<Row> Order(IQueryable<Row> rows, ListingReadFilter filter)
    {
        var ordered = filter.Sort switch
        {
            "registration" => OrderBy(rows, row => row.Vehicle.RegistrationPlate, filter.Direction),
            "vehicle" => OrderBy(rows, row => row.Vehicle.ManufactureYear + " " + row.Vehicle.Make + " " +
                row.Vehicle.Model + " " + row.Vehicle.Trim, filter.Direction),
            "branch" => OrderBy(rows, row => row.BranchName, filter.Direction),
            "inspection" => OrderBy(rows, row => row.Vehicle.Status, filter.Direction),
            "price" => OrderPrice(rows, filter.Direction),
            "publishedPrice" => OrderApprovedPrice(rows, filter.Direction),
            "activity" => OrderActivity(rows, filter.Direction),
            "publication" => OrderPublication(rows, filter.Direction),
            _ => rows.OrderBy(row => row.BranchName).ThenBy(row => row.Vehicle.RegistrationPlate)
        };
        return ordered.ThenBy(row => row.Vehicle.Id);
    }

    private static IQueryable<Row> FilterPublication(IQueryable<Row> rows, string publication) => publication switch
    {
        "published" => rows.Where(row => row.IsPublic),
        "changes" => rows.Where(row => row.IsPublic && row.Draft != null && row.Publication != null &&
            row.Draft.Id != row.Publication.RevisionId),
        "withdrawn" => rows.Where(row => !row.IsPublic && row.IsWithdrawn),
        "unavailable" => rows.Where(row => !row.IsPublic && !row.IsWithdrawn && row.Publication != null),
        "draft" => rows.Where(row => !row.IsPublic && !row.IsWithdrawn && row.Publication == null && row.Draft != null),
        "unprepared" => rows.Where(row => !row.IsPublic && !row.IsWithdrawn && row.Publication == null && row.Draft == null),
        _ => rows
    };

    private static IOrderedQueryable<Row> OrderBy<TKey>(IQueryable<Row> rows,
        Expression<Func<Row, TKey>> key, string direction)
    {
        return direction == "desc" ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    private static IOrderedQueryable<Row> OrderPrice(IQueryable<Row> rows, string direction)
    {
        // Unprepared prices remain last in either direction.
        var ordered = rows.OrderBy(row => row.Draft == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.Draft!.VehiclePrice)
            : ordered.ThenBy(row => row.Draft!.VehiclePrice);
    }

    private static IOrderedQueryable<Row> OrderApprovedPrice(IQueryable<Row> rows, string direction)
    {
        var ordered = rows.OrderBy(row => row.Approved == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.Approved!.VehiclePrice)
            : ordered.ThenBy(row => row.Approved!.VehiclePrice);
    }

    private static IOrderedQueryable<Row> OrderActivity(IQueryable<Row> rows, string direction)
    {
        var ordered = rows.OrderBy(row => row.ActivityAt == null);
        return direction == "desc"
            ? ordered.ThenByDescending(row => row.ActivityAt)
            : ordered.ThenBy(row => row.ActivityAt);
    }

    private static IOrderedQueryable<Row> OrderPublication(IQueryable<Row> rows, string direction)
    {
        // Follow preparation/publication progression, with withdrawn listings last when ascending.
        if (direction == "desc")
        {
            return rows.OrderByDescending(row => row.IsWithdrawn).ThenByDescending(row => row.IsPublic)
                .ThenByDescending(row => row.Publication != null).ThenByDescending(row => row.Draft != null)
                .ThenByDescending(row => row.IsPublic && row.Draft!.Id != row.Publication!.RevisionId);
        }

        return rows.OrderBy(row => row.IsWithdrawn).ThenBy(row => row.IsPublic)
            .ThenBy(row => row.Publication != null).ThenBy(row => row.Draft != null)
            .ThenBy(row => row.IsPublic && row.Draft!.Id != row.Publication!.RevisionId);
    }

    public async Task<ListingWorkspaceResult> WorkspaceAsync(Guid actorId, Guid vehicleId, CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => WorkspaceSnapshotAsync(actorId, vehicleId, cancellationToken),
            new ListingWorkspaceResult(ListingStatus.Unavailable), cancellationToken);
    }

    private async Task<ListingWorkspaceResult> WorkspaceSnapshotAsync(Guid actorId, Guid vehicleId,
        CancellationToken cancellationToken)
    {
        var actor = await ListingReadScope.ReadActorAsync(database, actorId, null, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        var now = await database.DatabaseNowAsync(cancellationToken);
        var rows = Rows(actor.BranchId, now).Where(row => row.Vehicle.Id == vehicleId);
        if (!await rows.AnyAsync(cancellationToken))
        {
            return new(ListingStatus.NotFound);
        }

        await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        var vehicle = await rows.Select(Item).SingleAsync(cancellationToken);
        var draft = await database.ListingRevisions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.RevisionId, cancellationToken);
        var approved = await database.ListingRevisions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.PublishedRevisionId, cancellationToken);
        var publication = await database.ListingPublications.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == vehicle.PublicationId, cancellationToken);
        var withdrawal = await database.ListingWithdrawals.AsNoTracking()
            .SingleOrDefaultAsync(row => row.PublicationId == vehicle.PublicationId, cancellationToken);
        var currentSigned = await (from signed in database.InspectionSignedRevisions.AsNoTracking()
                                   join inspection in database.InspectionDrafts.AsNoTracking() on signed.InspectionId equals inspection.Id
                                   where inspection.VehicleId == vehicleId && inspection.IsSigned && inspection.Revision == signed.Revision
                                   select new { signed.Id, signed.InspectionId, signed.Revision }).SingleOrDefaultAsync(cancellationToken);
        var report = currentSigned is null ? null : new ListingSignedReport(currentSigned.InspectionId, currentSigned.Revision);
        var vin = await database.Vehicles.AsNoTracking().Where(item => item.Id == vehicleId).Select(item => item.Vin)
            .SingleAsync(cancellationToken);
        var hold = await (from reservation in database.Reservations.AsNoTracking()
                          join sale in database.SalesOrders.AsNoTracking() on reservation.Id equals sale.Id
                          where reservation.VehicleId == vehicleId && reservation.IsActive
                          select new ListingHold(reservation.Buyer.LegalName,
                              database.Users.Where(user => user.Id == sale.AgentId).Select(user => user.Email).FirstOrDefault(),
                              sale.DueAt, reservation.CustomerId, sale.AgentId)).SingleOrDefaultAsync(cancellationToken);
        var pendingDeposit = await (from request in database.ReservationDepositRequests.AsNoTracking()
                                        .Where(ReservationDepositRequest.PendingAt(now))
                                    join profile in database.CustomerProfiles.AsNoTracking() on request.CustomerId equals profile.UserId
                                    where request.VehicleId == vehicleId
                                    select new ListingPendingDeposit(request.Id, profile.LegalName ?? PersonLabels.FormerCustomer, request.CustomerId,
                                        request.ExpiresAt)).FirstOrDefaultAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(vehicle, draft, approved, publication, withdrawal, currentSigned?.Id, report, vin,
            hold, pendingDeposit));
    }

    public async Task<ListingPhotoPageResult> PhotosAsync(Guid actorId, Guid vehicleId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || !Pagination.IsValid(page, pageSize))
        {
            return new(ListingStatus.Invalid);
        }

        return await ReservationReadSnapshot.ReadAsync(database,
            () => PhotosSnapshotAsync(actorId, vehicleId, page, pageSize, cancellationToken),
            new ListingPhotoPageResult(ListingStatus.Unavailable), cancellationToken);
    }

    private async Task<ListingPhotoPageResult> PhotosSnapshotAsync(Guid actorId, Guid vehicleId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var actor = await ListingReadScope.ReadActorAsync(database, actorId, null, cancellationToken);
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

        await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        var photos = database.ListingPhotos.AsNoTracking().Where(row => row.VehicleId == vehicleId);
        var total = await photos.CountAsync(cancellationToken);
        var items = await photos.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id)
            .Select(row => new ListingPhotoItem(row.Id, row.ContentType, row.Bytes, row.Width, row.Height, row.CreatedAt, row.AvailableAt))
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        return new(ListingStatus.Succeeded, new(Array.AsReadOnly(items), total, page, pageSize));
    }

    private IQueryable<Row> Rows(Guid? branchId, DateTimeOffset now)
    {
        var latest = database.ListingRevisions.AsNoTracking().Where(revision =>
            !database.ListingRevisions.Any(later => later.VehicleId == revision.VehicleId && later.Version > revision.Version));
        var publications = from publication in database.ListingPublications.AsNoTracking()
                           join revision in database.ListingRevisions on publication.RevisionId equals revision.Id
                           where !database.ListingPublications.Any(later => later.VehicleId == publication.VehicleId &&
                               database.ListingRevisions.Any(r => r.Id == later.RevisionId && r.Version > revision.Version))
                           select publication;
        var publicRows = PublicListingQuery.Read(database, now);
        var eligiblePublications = PublicListingQuery.Published(database);
        var activities = database.ListingRevisions.Select(item => new { item.VehicleId, item.RecordedAt })
            .Concat(database.ListingPublications.Select(item => new { item.VehicleId, item.RecordedAt }))
            .Concat(database.ListingWithdrawals.Select(item => new { item.VehicleId, item.RecordedAt }))
            .GroupBy(item => item.VehicleId)
            .Select(group => new { VehicleId = group.Key, RecordedAt = group.Max(item => item.RecordedAt) });
        return from vehicle in database.Vehicles.AsNoTracking()
               join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
               join saved in latest on vehicle.Id equals saved.VehicleId into drafts
               from draft in drafts.DefaultIfEmpty()
               join approved in publications on vehicle.Id equals approved.VehicleId into approvals
               from publication in approvals.DefaultIfEmpty()
               join revision in database.ListingRevisions on
                   (publication == null ? (Guid?)null : publication.RevisionId) equals (Guid?)revision.Id into approvedRevisions
               from approvedRevision in approvedRevisions.DefaultIfEmpty()
               join removed in database.ListingWithdrawals on
                   (publication == null ? (Guid?)null : publication.Id) equals (Guid?)removed.PublicationId into withdrawals
               from withdrawal in withdrawals.DefaultIfEmpty()
               join recorded in activities on vehicle.Id equals recorded.VehicleId into events
               from activity in events.DefaultIfEmpty()
               where branch.IsActive && (branchId == null || vehicle.BranchId == branchId)
               select new Row
               {
                   Vehicle = vehicle,
                   Draft = draft,
                   Publication = publication,
                   Approved = approvedRevision,
                   Withdrawal = withdrawal,
                   ActivityAt = activity == null ? null : activity.RecordedAt,
                   BranchName = branch.Name,
                   IsWithdrawn = withdrawal != null,
                   IsReserved = database.Reservations.Any(item => item.VehicleId == vehicle.Id && item.IsActive),
                   HasCurrentPublicationInspection = eligiblePublications.Any(row => row.Vehicle.Id == vehicle.Id),
                   IsPublic = publicRows.Any(row => row.Vehicle.Id == vehicle.Id)
               };
    }

    private sealed class Row
    {
        public required Vehicle Vehicle { get; init; }
        public required ListingRevision? Draft { get; init; }
        public required ListingPublication? Publication { get; init; }
        public required ListingRevision? Approved { get; init; }
        public required ListingWithdrawal? Withdrawal { get; init; }
        public DateTimeOffset? ActivityAt { get; init; }
        public required string BranchName { get; init; }
        public bool IsWithdrawn { get; init; }
        public bool IsPublic { get; init; }
        public bool IsReserved { get; init; }
        public bool HasCurrentPublicationInspection { get; init; }
    }
}
