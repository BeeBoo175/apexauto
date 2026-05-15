using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleReadScope
{
    public static async Task<(SaleStatus Status, SaleStaff? Actor, SalesOrder? Sale)> ReadAsync(
        DealershipDbContext database, Guid actorId, Guid saleId, CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.ReadAsync(database, actorId, cancellationToken);
        var branchActive = await SaleStaffLocks.BranchActiveAsync(database, actor, cancellationToken);
        if (!SaleAccess.IsEligibleStaff(actor, branchActive))
        {
            return (SaleStatus.Forbidden, null, null);
        }

        var sale = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == saleId,
            cancellationToken);
        return sale is null || !SaleAccess.CanSee(actor!, sale)
            ? (SaleStatus.NotFound, null, null)
            : (SaleStatus.Succeeded, actor, sale);
    }
}
