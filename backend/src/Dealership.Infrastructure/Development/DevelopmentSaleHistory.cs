using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Builds finished sales of dated listings at their historical times inside the snapshot transaction, mirroring the
// canonical claim, terms, submission, approval, settlement and handover commands, which can only stamp the database
// clock. Sales that stay open for staff, or that acquire a trade-in vehicle, remain on the canonical commands.
public sealed class DevelopmentSaleHistory(DealershipDbContext database, DevelopmentReservationHistory holds)
{
    private static readonly TimeSpan MaximumStep = TimeSpan.FromMinutes(30);

    public static IReadOnlyList<DevelopmentSaleScenario> All { get; } = Array.AsReadOnly(
        DevelopmentSaleScenarios.All.Concat(DevelopmentSaleVolumeManifest.All).ToArray());

    // The first volume sale stays on canonical commands so interrupted-claim recovery is proven on every seed.
    public static DevelopmentSaleScenario CanonicalVolumeSale => DevelopmentSaleVolumeManifest.All[0];

    // A sale that stays with staff keeps its customer's one active hold, so it is dated only when that customer
    // holds nothing else in the snapshot; trade-ins and released-first handling stay on canonical commands.
    public static bool Covers(DevelopmentSaleScenario scenario) =>
        scenario != CanonicalVolumeSale && scenario.TradeInAppointmentId is null && !scenario.ReleasedFirst &&
        scenario.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered or SalesOrderStatus.PendingApproval or
            SalesOrderStatus.Approved or SalesOrderStatus.Handling &&
        DevelopmentWorkshopHistory.Covers(scenario.Hold.Listing) &&
        (scenario.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered || HoldsOnlyThisReservation(scenario));

    private static bool HoldsOnlyThisReservation(DevelopmentSaleScenario scenario) =>
        DevelopmentReservationScenarios.All.Concat(DevelopmentSaleVolumeManifest.All.Select(item => item.Hold))
            .Count(item => item.RegistrationKey == scenario.Hold.RegistrationKey) == 1;

    public static IReadOnlyList<DevelopmentSaleScenario> For(DevelopmentInspectionScenario listing) =>
        [.. All.Where(item => item.Hold.Listing.Id == listing.Id && Covers(item))];

    public async Task AddSaleAsync(DevelopmentSaleScenario scenario, DateTimeOffset publishedAt, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gap = now - publishedAt;
        var heldAt = DevelopmentReservationHistory.Instant(publishedAt, gap, scenario.HeldStep, 10);
        // Steps stay well inside the 48-hour hold, so the sale is claimed before it could expire.
        var step = TimeSpan.FromTicks(Math.Min(MaximumStep.Ticks, (gap / 40).Ticks));
        await holds.AddHoldAsync(scenario.Hold, heldAt, cancellationToken);

        var sale = await database.SalesOrders.SingleAsync(item => item.Id == scenario.ReservationId, cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleAsync(cancellationToken);
        var agent = scenario.AgentId;
        await RecordAsync(sale.Claim(scenario.OperationId("claim"), agent, agent, heldAt + step), cancellationToken);

        var request = await DevelopmentSaleFixtures.TermsAsync(database, scenario, cancellationToken);
        var input = new SaleTermsInput(await WarrantyAsync(request.WarrantyPackageId, cancellationToken),
            request.NegotiatedDiscount, null);
        await RecordAsync(sale.SaveTerms(request.OperationId, input, settings, agent, heldAt + step * 2),
            cancellationToken);

        if (scenario.Target == SalesOrderStatus.Handling && !scenario.RejectedOnce)
        {
            database.ChangeTracker.Clear();
            return;
        }

        var submittedAt = heldAt + step * 3;
        var number = sale.OrderNumber ??
            await YearlyNumberAllocator.NextAsync<DealNumberCounter>(database, submittedAt.Year, cancellationToken);
        await RecordAsync(sale.Submit(scenario.OperationId("submit"), settings, number, agent, submittedAt),
            cancellationToken);

        if (scenario.RejectedOnce)
        {
            await RecordAsync(sale.Reject(scenario.OperationId("reject"), scenario.ManagerId,
                DevelopmentSaleFixtures.RejectionReason, heldAt + step * 4), cancellationToken);
            database.ChangeTracker.Clear();
            return;
        }

        var next = 4;
        if (scenario.ManagerApproval && scenario.Target != SalesOrderStatus.PendingApproval)
        {
            await RecordAsync(sale.Approve(scenario.OperationId("approve"), scenario.ManagerId, heldAt + step * next++),
                cancellationToken);
        }

        if (scenario.Target is SalesOrderStatus.PendingApproval or SalesOrderStatus.Approved)
        {
            database.ChangeTracker.Clear();
            return;
        }

        await SettleAsync(scenario, sale, agent, heldAt + step * next++, cancellationToken);
        if (scenario.Target == SalesOrderStatus.Delivered)
        {
            await DeliverAsync(scenario, sale, agent, heldAt + step * next, cancellationToken);
        }

        database.ChangeTracker.Clear();
    }

    private async Task<SaleWarranty?> WarrantyAsync(Guid? packageId, CancellationToken cancellationToken)
    {
        if (packageId is null)
        {
            return null;
        }

        var package = await database.WarrantyPackages.AsNoTracking()
            .SingleAsync(item => item.Id == packageId, cancellationToken);
        return new SaleWarranty(package.Id, package.Name, package.Price);
    }

    private async Task SettleAsync(DevelopmentSaleScenario scenario, SalesOrder sale, Guid agent, DateTimeOffset settledAt,
        CancellationToken cancellationToken)
    {
        var review = (await DevelopmentSaleFixtures.ReviewAsync(database, sale, cancellationToken)).Normalize();
        var payment = DevelopmentSaleFixtures.Payment(scenario, sale.BalancePayable, sale.CreatedAt);
        var entry = sale.Settle(scenario.OperationId("settle"), payment, null, agent, settledAt);
        entry.CaptureSettlementReview(review);
        database.SalesOrderEvents.Add(entry);
        await SaleSettlementDocuments.IssueAsync(database, sale, entry, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await CheckDeferredConstraintsAsync(cancellationToken);
    }

    private async Task DeliverAsync(DevelopmentSaleScenario scenario, SalesOrder sale, Guid agent,
        DateTimeOffset deliveredAt, CancellationToken cancellationToken)
    {
        var mileage = await database.Vehicles.AsNoTracking().Where(item => item.Id == sale.VehicleId)
            .Select(item => item.MileageKilometres).SingleAsync(cancellationToken);
        var entry = sale.Deliver(scenario.OperationId("handover"), new SaleHandoverEvidence(mileage, 2, true, true, true),
            mileage, agent, deliveredAt);
        database.SalesOrderEvents.Add(entry);
        await SaleHandoverDocuments.IssueAsync(database, sale, entry, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    // Settlement review is a deferred check of the sale as it stands at that command; production commits each command
    // alone, so the snapshot checks it here before the sale moves on to handover.
    private async Task CheckDeferredConstraintsAsync(CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE", cancellationToken);
        await database.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED", cancellationToken);
    }

    private async Task RecordAsync(SalesOrderEvent entry, CancellationToken cancellationToken)
    {
        database.SalesOrderEvents.Add(entry);
        await database.SaveChangesAsync(cancellationToken);
    }
}
