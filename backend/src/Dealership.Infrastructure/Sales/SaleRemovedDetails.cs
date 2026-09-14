using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// True once the retention period removed the buyer's details from a sale's reservation copy or issued documents.
internal static class SaleRemovedDetails
{
    public static async Task<bool> AnyAsync(DealershipDbContext database, Guid saleId,
        CancellationToken cancellationToken)
    {
        var emailRemoved = await database.Reservations.AsNoTracking()
            .AnyAsync(item => item.Id == saleId && item.Buyer.Email == null, cancellationToken);
        if (emailRemoved)
        {
            return true;
        }

        var purchaseOrders = await database.SaleDocuments.AsNoTracking().Where(item =>
                item.SalesOrderId == saleId && item.Kind == SaleDocumentKind.PurchaseOrder &&
                item.CorrectionOperationId == null)
            .ToListAsync(cancellationToken);
        return purchaseOrders.Any(item => item.Contents().Review.BuyerDetailsRemoved);
    }
}
