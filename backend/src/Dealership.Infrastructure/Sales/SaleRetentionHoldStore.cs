using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleRetentionHoldStore(DealershipDbContext database, RetentionSignal retention)
    : ISaleRetentionHoldStore
{
    public async Task<SaleRetentionHoldResult> SetByNumberAsync(Guid actorId, string reservationNumber,
        SaleRetentionHoldRequest request, CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(SaleRetentionHoldStatus.Forbidden);
        }

        var saleId = await database.Reservations.AsNoTracking().Where(item => item.Number == reservationNumber)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        return saleId is null
            ? new(SaleRetentionHoldStatus.NotFound)
            : await SetForSaleAsync(actorId, saleId.Value, request, cancellationToken);
    }

    public async Task<SaleRetentionHoldListResult> ListAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(SaleRetentionHoldStatus.Forbidden);
        }

        var holds = await (from sale in database.SalesOrders.AsNoTracking()
                           join reservation in database.Reservations.AsNoTracking() on sale.Id equals reservation.Id
                           where sale.RetentionHoldUntil != null
                           orderby sale.RetentionHoldUntil, reservation.Number
                           select new SaleRetentionHoldRow(sale.Id, reservation.Number, sale.RetentionHoldUntil!.Value,
                               sale.RetentionHoldReason!))
            .Take(100).ToListAsync(cancellationToken);
        return new(SaleRetentionHoldStatus.Succeeded, holds);
    }

    private async Task<SaleRetentionHoldResult> SetForSaleAsync(Guid actorId, Guid saleId, SaleRetentionHoldRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(SaleRetentionHoldStatus.Forbidden);
        }

        var vehicleId = await database.SalesOrders.AsNoTracking().Where(item => item.Id == saleId)
            .Select(item => (Guid?)item.VehicleId).SingleOrDefaultAsync(cancellationToken);
        if (vehicleId is null)
        {
            return new(SaleRetentionHoldStatus.NotFound);
        }

        // The vehicle is locked before the sale, as every sale command does.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {vehicleId} FOR UPDATE", cancellationToken);
        var sale = await database.SalesOrders.FromSqlInterpolated(
            $"SELECT * FROM \"SalesOrders\" WHERE \"Id\" = {saleId} FOR UPDATE").SingleAsync(cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        if (request.Until is null)
        {
            sale.ReleaseRetentionHold();
        }
        else if (SalesOrder.ValidHold(request.Until.Value, request.Reason, now))
        {
            sale.PlaceRetentionHold(request.Until.Value, request.Reason!, actorId, now);
        }
        else
        {
            return new(SaleRetentionHoldStatus.Invalid);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Releasing a hold can make records eligible at once, so the executor looks again after the commit.
        retention.Request();
        return new(SaleRetentionHoldStatus.Succeeded,
            new(sale.Id, sale.RetentionHoldUntil, sale.RetentionHoldReason, sale.RetentionHoldById));
    }
}
