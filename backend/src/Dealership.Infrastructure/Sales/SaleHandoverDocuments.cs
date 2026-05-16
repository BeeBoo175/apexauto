using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleHandoverDocuments
{
    public static async Task IssueAsync(DealershipDbContext database, SalesOrder sale, SalesOrderEvent entry,
        CancellationToken cancellationToken)
    {
        // Delivery retains the reviewed settlement identity, never today's mutable buyer or branch profile.
        var original = await database.SaleDocuments.AsNoTracking().SingleAsync(item =>
            item.SalesOrderId == sale.Id && item.Kind == SaleDocumentKind.PurchaseOrder, cancellationToken);
        var contents = original.Contents() with { Sale = entry.Snapshot };
        var year = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(entry.RecordedAt, "Asia/Kuala_Lumpur").Year;
        var number = await YearlyNumberAllocator.NextAsync<HandoverNumberCounter>(database, year, cancellationToken);
        database.SaleDocuments.Add(new SaleDocument(sale.Id, entry.Id, SaleDocumentKind.HandoverNote, number,
            entry.RecordedAt, contents));
        var recipient = await database.Reservations.AsNoTracking().Where(item => item.Id == sale.Id)
            .Select(item => item.Buyer.Email).SingleAsync(cancellationToken);
        database.SaleDeliveryMailOutbox.Add(new SaleDeliveryMail(sale.Id, entry.Id, recipient, entry.RecordedAt));
    }
}
