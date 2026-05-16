using Dealership.Application.Sales;
using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleReadScope
{
    public static async Task<(SaleResultStatus Status, SaleStaff? Actor, SalesOrder? Sale)> ReadAsync(
        DealershipDbContext database, Guid actorId, Guid saleId, CancellationToken cancellationToken,
        bool allowCustomer = false)
    {
        var actor = await SaleStaffLocks.FindAsync(database, actorId, cancellationToken);
        var branchActive = await SaleStaffLocks.BranchActiveAsync(database, actor, cancellationToken);
        var customer = allowCustomer && actor is { IsActive: true, Role: AccountRoles.Customer };
        if (!customer && !SaleAccess.IsEligibleStaff(actor, branchActive))
        {
            return (SaleResultStatus.Forbidden, null, null);
        }

        var sale = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == saleId,
            cancellationToken);
        var visible = sale is not null && (customer ? sale.CustomerId == actorId : SaleAccess.CanSee(actor!, sale));
        return !visible
            ? (SaleResultStatus.NotFound, null, null)
            : (SaleResultStatus.Succeeded, actor, sale);
    }
}
