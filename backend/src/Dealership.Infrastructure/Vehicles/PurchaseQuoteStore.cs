using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PurchaseQuoteStore(DealershipDbContext database) : IPurchaseQuoteStore
{
    public Task<PurchaseQuoteContext> ReadAsync(Guid vehicleId, string? code, CancellationToken cancellationToken)
    {
        return ReservationReadSnapshot.ReadAsync(database,
            () => ReadSnapshotAsync(vehicleId, code, cancellationToken),
            new PurchaseQuoteContext(null, null, null, default) { IsAvailable = false }, cancellationToken);
    }

    private async Task<PurchaseQuoteContext> ReadSnapshotAsync(Guid vehicleId, string? code, CancellationToken cancellationToken)
    {
        await ReservationReadSnapshot.ReconcileAsync(database, vehicleId, cancellationToken);
        var listing = await PublicListingQuery.Read(database).Where(row => row.Vehicle.Id == vehicleId)
            .Select(row => new PurchaseListingPrice(row.Revision.VehiclePrice, row.Revision.RoadTax))
            .SingleOrDefaultAsync(cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var promo = code is null ? null : await database.PromoCodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        return new(listing, settings, promo, now);
    }
}
