using System.Data;
using Dealership.Application.Common;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingHistoryStore(DealershipDbContext database) : IListingHistoryStore
{
    public async Task<ListingHistoryResult> ReadAsync(Guid actorId, Guid vehicleId, ListingHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || !query.IsValid)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var (status, vehicle) = await ReadScopeAsync(actorId, vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new(status);
        }

        var drafts = from revision in database.ListingRevisions.AsNoTracking()
                     where revision.VehicleId == vehicleId
                     select new
                     {
                         revision.Id, Kind = "DraftSaved", RevisionId = revision.Id, revision.Version,
                         revision.ActorId, revision.RecordedAt, Reason = (string?)null, Order = 0
                     };
        var publications = from publication in database.ListingPublications.AsNoTracking()
                           join revision in database.ListingRevisions on publication.RevisionId equals revision.Id
                           where publication.VehicleId == vehicleId
                           select new
                           {
                               publication.Id, Kind = "PublicationApproved", publication.RevisionId, revision.Version,
                               publication.ActorId, publication.RecordedAt, Reason = (string?)null, Order = 1
                           };
        var withdrawals = from withdrawal in database.ListingWithdrawals.AsNoTracking()
                          join publication in database.ListingPublications on withdrawal.PublicationId equals publication.Id
                          join revision in database.ListingRevisions on publication.RevisionId equals revision.Id
                          where withdrawal.VehicleId == vehicleId
                          select new
                          {
                              withdrawal.Id, Kind = "ListingWithdrawn", publication.RevisionId, revision.Version,
                              withdrawal.ActorId, withdrawal.RecordedAt, Reason = (string?)withdrawal.Reason, Order = 2
                          };
        var events = drafts.Concat(publications).Concat(withdrawals);
        var total = await events.CountAsync(cancellationToken);
        var rows = await (from entry in events
                          join actor in database.Users.AsNoTracking() on entry.ActorId equals actor.Id
                          select new { Entry = entry, ActorIdentity = actor.UserName ?? actor.Id.ToString() })
            .OrderByDescending(row => row.Entry.RecordedAt).ThenByDescending(row => row.Entry.Order)
            .ThenByDescending(row => row.Entry.Id)
            .ReadPageAsync(query.Page, query.PageSize, total, cancellationToken);
        var items = rows.Select(row => new ListingHistoryEntry(row.Entry.Id, row.Entry.Kind,
            row.Entry.RevisionId, row.Entry.Version, row.Entry.ActorId, row.ActorIdentity,
            row.Entry.RecordedAt, row.Entry.Reason)).ToArray();

        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded,
            new(vehicleId, vehicle.BranchId, Array.AsReadOnly(items), total, query.Page, query.PageSize));
    }

    public async Task<ListingRevisionReadResult> RevisionAsync(Guid actorId, Guid vehicleId, Guid revisionId,
        CancellationToken cancellationToken)
    {
        if (vehicleId == Guid.Empty || revisionId == Guid.Empty)
        {
            return new(ListingStatus.Invalid);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var (status, vehicle) = await ReadScopeAsync(actorId, vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new(status);
        }

        var revision = await database.ListingRevisions.AsNoTracking().SingleOrDefaultAsync(
            row => row.Id == revisionId && row.VehicleId == vehicleId && row.BranchId == vehicle.BranchId, cancellationToken);
        if (revision is null)
        {
            return new(ListingStatus.NotFound);
        }

        var publication = await database.ListingPublications.AsNoTracking().SingleOrDefaultAsync(
            row => row.RevisionId == revisionId && row.VehicleId == vehicleId, cancellationToken);
        ListingSignedReport? report = null;
        ListingWithdrawal? withdrawal = null;
        if (publication is not null)
        {
            report = await database.InspectionSignedRevisions.AsNoTracking()
                .Where(signed => signed.Id == publication.SignedRevisionId)
                .Select(signed => new ListingSignedReport(signed.InspectionId, signed.Revision))
                .SingleOrDefaultAsync(cancellationToken);
            withdrawal = await database.ListingWithdrawals.AsNoTracking().SingleOrDefaultAsync(
                row => row.PublicationId == publication.Id && row.VehicleId == vehicleId, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, new(vehicleId, vehicle.BranchId, revision, report, publication, withdrawal));
    }

    private async Task<(ListingStatus Status, Vehicle? Vehicle)> ReadScopeAsync(Guid actorId, Guid vehicleId,
        CancellationToken cancellationToken)
    {
        var actor = await ListingReadScope.ReadActorAsync(database, actorId, null, cancellationToken);
        if (actor is null)
        {
            return (ListingStatus.Forbidden, null);
        }

        var vehicle = await database.Vehicles.AsNoTracking().SingleOrDefaultAsync(row => row.Id == vehicleId &&
            (actor.BranchId == null || row.BranchId == actor.BranchId) &&
            database.Branches.Any(branch => branch.Id == row.BranchId && branch.IsActive), cancellationToken);
        return vehicle is null ? (ListingStatus.NotFound, null) : (ListingStatus.Succeeded, vehicle);
    }
}
