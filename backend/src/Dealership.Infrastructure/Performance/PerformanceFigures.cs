using Dealership.Application.Performance;
using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Performance;

// Totals and weekly flow shared by the headline, the branch comparison and the stock flow, so they cannot disagree.
internal static class PerformanceFigures
{
    internal static async Task<PerformancePeriodTotals> TotalsAsync(DealershipDbContext database, Guid? branchId,
        DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var delivered = PerformanceScopes.SalesInScope(database, branchId)
            .Where(order => order.DeliveredAt >= periodStart && order.DeliveredAt < periodEnd);
        var deliveredCount = await delivered.CountAsync(cancellationToken);
        var deliveredValue = await delivered
            .Select(SalesOrderValue.Projection)
            .SumAsync(order => (decimal?)order.Subtotal,
                cancellationToken) ?? 0m;

        var firstListings = database.ListingPublications.AsNoTracking()
            .Where(publication => branchId == null || publication.BranchId == branchId)
            .GroupBy(publication => publication.VehicleId)
            .Select(group => new { VehicleId = group.Key, ListedAt = group.Min(publication => publication.RecordedAt) });
        var listed = from listing in firstListings
                     join checkIn in database.AppointmentCheckIns.AsNoTracking() on listing.VehicleId equals checkIn.VehicleId
                     where listing.ListedAt >= periodStart && listing.ListedAt < periodEnd
                     select (listing.ListedAt - checkIn.CheckedInAt).TotalSeconds;
        var listedCount = await listed.CountAsync(cancellationToken);
        var averageSeconds = listedCount == 0 ? (double?)null : await listed.AverageAsync(cancellationToken);

        // A no-show is a booked day that has ended with no cancellation and no check-in, corrected ones included.
        var todayStartsAt = AppointmentDay.StartUtc(now);
        var noShows = await database.IntakeAppointments.AsNoTracking()
            .Where(appointment => (branchId == null || appointment.BranchId == branchId) &&
                appointment.CancelledAt == null && appointment.StartsAt >= periodStart && appointment.StartsAt < periodEnd &&
                appointment.StartsAt < todayStartsAt &&
                !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == appointment.Id))
            .CountAsync(cancellationToken);
        return new(deliveredCount, deliveredValue, listedCount, averageSeconds / TimeSpan.SecondsPerDay, noShows);
    }

    // The weekly flow of one branch, or of every branch in scope when none is chosen.
    internal static async Task<PerformanceStockWeek[]> WeeklyFlowAsync(DealershipDbContext database,
        Guid? branchId, PerformanceQuery query,
        CancellationToken cancellationToken)
    {
        var checkIns = await database.AppointmentCheckIns.AsNoTracking()
            .Where(item => item.CorrectionRecordedAt == null && item.CheckedInAt >= query.From &&
                item.CheckedInAt < query.Until && (branchId == null || item.BranchId == branchId))
            .Select(item => item.CheckedInAt)
            .ToListAsync(cancellationToken);
        var deliveries = await PerformanceScopes.SalesInScope(database, branchId)
            .Where(order => order.DeliveredAt >= query.From && order.DeliveredAt < query.Until)
            .Select(SalesOrderValue.Projection)
            .Select(order => new
            {
                At = order.DeliveredAt!.Value,
                Value = order.Subtotal,
            })
            .ToListAsync(cancellationToken);

        var weeks = new SortedDictionary<DateOnly, (int CheckIns, int Deliveries, decimal Value)>();
        for (var week = WeekStart(query.From); week < MalaysiaDate(query.Until); week = week.AddDays(7))
        {
            weeks[week] = (0, 0, 0m);
        }

        foreach (var at in checkIns)
        {
            var week = WeekStart(at);
            weeks[week] = weeks[week] with { CheckIns = weeks[week].CheckIns + 1 };
        }

        foreach (var delivery in deliveries)
        {
            var week = WeekStart(delivery.At);
            weeks[week] = weeks[week] with
            {
                Deliveries = weeks[week].Deliveries + 1,
                Value = weeks[week].Value + delivery.Value,
            };
        }

        return [.. weeks.Select(item =>
            new PerformanceStockWeek(item.Key, item.Value.CheckIns, item.Value.Deliveries, item.Value.Value))];
    }

    private static DateOnly MalaysiaDate(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, DealershipLocale.Malaysia).DateTime);
    }

    private static DateOnly WeekStart(DateTimeOffset instant)
    {
        var date = MalaysiaDate(instant);
        var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-sinceMonday);
    }
}
