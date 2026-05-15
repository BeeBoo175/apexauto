using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleReadScope
{
    public static async Task<(SaleResultStatus Status, SaleStaff? Actor, SalesOrder? Sale)> ReadAsync(
        DealershipDbContext database, Guid actorId, Guid saleId, CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.FindAsync(database, actorId, cancellationToken);
        var branchActive = await SaleStaffLocks.BranchActiveAsync(database, actor, cancellationToken);
        if (!SaleAccess.IsEligibleStaff(actor, branchActive))
        {
            return (SaleResultStatus.Forbidden, null, null);
        }

        var sale = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == saleId,
            cancellationToken);
        return sale is null || !SaleAccess.CanSee(actor!, sale)
            ? (SaleResultStatus.NotFound, null, null)
            : (SaleResultStatus.Succeeded, actor, sale);
    }
}
