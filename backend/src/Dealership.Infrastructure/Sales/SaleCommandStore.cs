using Dealership.Infrastructure.TestDrives;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleCommandStore(DealershipDbContext database, MailDeliverySignal mail) : ISaleCommandStore
{
    public async Task<ISaleCommandTransaction> LockAsync(Guid actorId, Guid saleId, Guid operationId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Replays of one operation are serialized so they all see a single outcome.
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"sale:" + operationId}, 0))", cancellationToken);
            var actor = await SaleStaffLocks.LockAsync(database, actorId, cancellationToken);
            var located = await database.SalesOrders.AsNoTracking()
                .Where(item => item.Id == saleId).Select(item => new { item.VehicleId, item.BranchId })
                .SingleOrDefaultAsync(cancellationToken);
            // Group actors have no assigned branch; serialize the sale's branch before its vehicle too.
            var branches = await database.Branches.FromSqlInterpolated($"""
                SELECT * FROM "Branches" WHERE "Id" IN ({actor?.BranchId}, {located?.BranchId})
                ORDER BY "Id" FOR UPDATE
                """).AsNoTracking().ToArrayAsync(cancellationToken);
            var branchActive = actor?.BranchId is not { } branchId ||
                branches.Any(branch => branch.Id == branchId && branch.IsActive);
            var existing = await database.SalesOrderEvents.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
            if (located is null)
            {
                return new SaleCommandTransaction(database, transaction,
                    new(actor, branchActive, null, existing, default), mail);
            }

            // The vehicle is locked before its hold and sale, as every reservation command does; an expired or
            // no longer sellable hold is released first so the decision below sees the authoritative state.
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {located.VehicleId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT release_unavailable_reservation({located.VehicleId})", cancellationToken);
            var sale = await database.SalesOrders.FromSqlInterpolated(
                $"SELECT * FROM \"SalesOrders\" WHERE \"Id\" = {saleId} FOR UPDATE").SingleAsync(cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            // The database clock can trail a sale stamped by another session by a few microseconds.
            now = now < sale.UpdatedAt ? sale.UpdatedAt : now;
            var occupied = await TestDriveOccupationRows.Active(database)
                .AnyAsync(item => item.VehicleId == sale.VehicleId, cancellationToken);
            var customerClosed = await database.Users.AsNoTracking()
                .AnyAsync(user => user.Id == sale.CustomerId && user.ClosedAt != null, cancellationToken);
            // Only a settled or delivered sale holds issued documents whose buyer details retention can remove.
            var detailsRemoved = sale.Status is SalesOrderStatus.Settled or SalesOrderStatus.Delivered &&
                await SaleRemovedDetails.AnyAsync(database, sale.Id, cancellationToken);
            return new SaleCommandTransaction(database, transaction,
                new(actor, branchActive, sale, existing, now, occupied, customerClosed, detailsRemoved), mail);
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
