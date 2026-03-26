using System.Data;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PurchaseQuoteStore(DealershipDbContext database) : IPurchaseQuoteStore
{
    public async Task<PurchaseQuoteContext> ReadAsync(Guid vehicleId, string? code, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var listing = await PublicListingQuery.Read(database).Where(row => row.Vehicle.Id == vehicleId)
            .Select(row => new PurchaseListingPrice(row.Revision.VehiclePrice, row.Revision.RoadTax))
            .SingleOrDefaultAsync(cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var promo = code is null ? null : await database.PromoCodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        var now = await database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(listing, settings, promo, now);
    }
}
