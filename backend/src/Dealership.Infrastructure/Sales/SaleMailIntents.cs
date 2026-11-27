using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleMailIntents
{
    // Settlement and handover each queue one customer mail, saved with the history entry that caused it.
    public static async Task AddAsync(DealershipDbContext database, SalesOrder sale, SalesOrderEvent entry,
        CancellationToken cancellationToken)
    {
        var recipient = await database.Reservations.AsNoTracking().Where(item => item.Id == sale.Id)
            .Select(item => item.Buyer.Email).SingleAsync(cancellationToken) ??
            throw new InvalidOperationException("Sale mail requires the buyer's recorded email.");
        database.SaleDeliveryMailOutbox.Add(new SaleDeliveryMail(sale.Id, entry.Id, recipient, entry.RecordedAt));
    }
}
