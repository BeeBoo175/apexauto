using System.Data;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingEstimateStore(DealershipDbContext database) : IListingEstimateStore
{
    public async Task<ListingEstimateContext> ReadAsync(Guid actorId, Guid vehicleId, Guid branchId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await ListingReadScope.ReadActorAsync(database, actorId, branchId, cancellationToken);
        if (actor is null)
        {
            return new(ListingStatus.Forbidden);
        }

        var exists = await database.Vehicles.AsNoTracking().AnyAsync(vehicle => vehicle.Id == vehicleId &&
            vehicle.BranchId == branchId && (actor.BranchId == null || vehicle.BranchId == actor.BranchId) &&
            database.Branches.Any(branch => branch.Id == vehicle.BranchId && branch.IsActive), cancellationToken);
        if (!exists)
        {
            return new(ListingStatus.NotFound);
        }

        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ListingStatus.Succeeded, settings);
    }
}
