using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

internal sealed class PublicListingRow
{
    public required Vehicle Vehicle { get; init; }
    public required ListingRevision Revision { get; init; }
    public required ListingPublication Publication { get; init; }
    public required string BranchName { get; init; }
}

internal static class PublicListingQuery
{
    // A car is available while it has neither an active hold nor a deposit request waiting for its customer.
    // `ownRequestId` is the confirming customer's request, which does not hold the car against its own confirmation.
    public static IQueryable<PublicListingRow> Read(DealershipDbContext database, Guid ownRequestId = default)
    {
        var now = DateTimeOffset.UtcNow;
        return Published(database).Where(row =>
            !database.Reservations.Any(item => item.VehicleId == row.Vehicle.Id && item.IsActive) &&
            !database.ReservationDepositRequests.Where(ReservationDepositRequest.PendingAt(now)).Any(item =>
                item.VehicleId == row.Vehicle.Id && item.Id != ownRequestId));
    }

    public static IQueryable<PublicListingRow> Published(DealershipDbContext database)
    {
        return from publication in database.ListingPublications.AsNoTracking()
               join revision in database.ListingRevisions.AsNoTracking() on publication.RevisionId equals revision.Id
               join vehicle in database.Vehicles.AsNoTracking() on publication.VehicleId equals vehicle.Id
               join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
               where branch.IsActive && vehicle.Status == VehicleStatus.Available &&
                   !database.ListingWithdrawals.Any(item => item.PublicationId == publication.Id) &&
                   !database.ListingPublications.Any(later => later.VehicleId == vehicle.Id &&
                       database.ListingRevisions.Any(r => r.Id == later.RevisionId && r.Version > revision.Version)) &&
                   database.InspectionSignedRevisions.Any(signed => signed.Id == publication.SignedRevisionId &&
                       database.InspectionDrafts.Any(draft => draft.VehicleId == vehicle.Id && draft.Id == signed.InspectionId &&
                           draft.IsSigned && draft.Revision == signed.Revision))
               select new PublicListingRow
               {
                   Vehicle = vehicle,
                   Revision = revision,
                   Publication = publication,
                   BranchName = branch.Name
               };
    }
}
