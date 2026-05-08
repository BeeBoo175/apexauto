using Dealership.Domain.Intake;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

// Which of the customer's own cars a sale may take in trade. The editor's choices and the save use this one rule.
internal static class SaleTradeIns
{
    // The editor offers the newest choices first and never more than this many.
    public const int OptionLimit = 25;

    // A car that was checked in, so its valuation was confirmed at the branch, and that is not the car being
    // bought. Cancelled appointments and no-shows were never checked in.
    public static IQueryable<IntakeAppointment> Eligible(DealershipDbContext database, SalesOrder sale) =>
        database.IntakeAppointments.AsNoTracking().Where(item =>
            item.OwnerId == sale.CustomerId && item.CancelledAt == null &&
            database.AppointmentCheckIns.Any(checkIn =>
                checkIn.AppointmentId == item.Id && checkIn.VehicleId != sale.VehicleId));

    // A choice another live sale already holds is not offered again.
    public static IQueryable<Guid?> TakenByOtherSales(DealershipDbContext database, Guid saleId) =>
        database.SalesOrders.Where(item => item.Id != saleId && item.TradeInAppointmentId != null &&
                item.Status != SalesOrderStatus.Expired && item.Status != SalesOrderStatus.Cancelled)
            .Select(item => item.TradeInAppointmentId);
}
