using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

internal static class SaleSettlementDocuments
{
    public static async Task<bool> ReviewAsync(DealershipDbContext database, SalesOrder sale,
        SaleSettlementReview review, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"CustomerProfiles\" WHERE \"UserId\" = {sale.CustomerId} FOR SHARE", cancellationToken);
        var nric = await database.CustomerProfiles.AsNoTracking().Where(item => item.UserId == sale.CustomerId)
            .Select(item => item.Nric).SingleAsync(cancellationToken);
        var reservationNric = await database.Reservations.AsNoTracking().Where(item => item.Id == sale.Id)
            .Select(item => item.Buyer.Nric).SingleAsync(cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == sale.VehicleId,
            cancellationToken);
        return review.BuyerNric == nric && review.BuyerNric == reservationNric &&
            review.Vehicle == SaleReviewedVehicle.Of(vehicle);
    }

    public static async Task IssueAsync(DealershipDbContext database, SalesOrder sale, SalesOrderEvent entry,
        CancellationToken cancellationToken)
    {
        var review = entry.SettlementReview ?? throw new InvalidOperationException("Settlement requires reviewed details.");
        var branch = await database.Branches.AsNoTracking().SingleAsync(item => item.Id == sale.BranchId,
            cancellationToken);
        var reservation = await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == sale.Id,
            cancellationToken);
        var contents = new SaleDocumentContents(sale.CustomerId, sale.BranchId, branch.Name, branch.Address,
            reservation.Number, review, entry.Snapshot);
        var year = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(entry.RecordedAt, "Asia/Kuala_Lumpur").Year;
        var receiptNumber = await YearlyNumberAllocator.NextAsync<ReceiptNumberCounter>(database, year,
            cancellationToken);
        database.SaleDocuments.AddRange(
            new SaleDocument(sale.Id, entry.Id, SaleDocumentKind.PurchaseOrder, sale.OrderNumber!, entry.RecordedAt, contents),
            new SaleDocument(sale.Id, entry.Id, SaleDocumentKind.PaymentReceipt, receiptNumber, entry.RecordedAt, contents));
        await SaleMailIntents.AddAsync(database, sale, entry, cancellationToken);
    }
}
