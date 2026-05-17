using Dealership.Application.Sales;
using Dealership.Application.Inspections;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Drives each demo sale through the same commands staff use. Every step has a fixed operation and base version,
// so a rerun replays completed steps instead of repeating them, and a sale changed by hand is reported, not reset.
public sealed class DevelopmentSaleFixtures(DealershipDbContext database, SaleHandlingService handling,
    SaleTermsService terms, SaleSettlementService settlement, SaleHandoverService handover,
    SaleDocumentCorrectionService corrections, IInspectionReportStore inspectionReports)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentSaleScenarios.All)
        {
            reports.Add(await RunOneAsync(seed, scenario, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentSaleScenario scenario,
        CancellationToken cancellationToken)
    {
        var label = $"Sale ({scenario.Hold.Listing.Intake.RegistrationPlate}; {scenario.Target})";
        var sale = await database.SalesOrders.AsNoTracking()
            .Where(item => item.Id == scenario.ReservationId)
            .Select(item => new { item.Status, item.Version }).SingleOrDefaultAsync(cancellationToken);
        if (sale is null)
        {
            return new(label, "hold missing; preserved", true);
        }

        // An open hold is only worked from its first version; one moved on by hand is reported, not reset.
        if (seed && sale.Status != scenario.Target && !await DriveAsync(scenario, cancellationToken))
        {
            return new(label, "sale changed elsewhere; preserved", true);
        }

        var status = await database.SalesOrders.Where(item => item.Id == scenario.ReservationId)
            .Select(item => item.Status).SingleAsync(cancellationToken);
        if (scenario.CorrectedBuyerAddress is not null && status == scenario.Target &&
            !await CorrectDocumentAsync(seed, scenario, cancellationToken))
        {
            return new(label, "document correction missing or changed elsewhere; preserved", true);
        }

        if (status is SalesOrderStatus.Settled or SalesOrderStatus.Delivered &&
            await database.SaleDocuments.CountAsync(item => item.SalesOrderId == scenario.ReservationId &&
                item.CorrectionOperationId == null,
                cancellationToken) != (status == SalesOrderStatus.Delivered ? 3 : 2))
        {
            return new(label, "settlement documents missing; preserved", true);
        }

        if (status == SalesOrderStatus.Delivered &&
            !await database.SaleDeliveryMailOutbox.AnyAsync(item => item.SalesOrderId == scenario.ReservationId &&
                item.DeliveryOperationId == scenario.OperationId("handover"), cancellationToken))
        {
            return new(label, "delivery mail missing; preserved", true);
        }

        if (status != scenario.Target)
        {
            return new(label, $"{status}; preserved", true);
        }

        var purchased = await database.SalesOrders.AsNoTracking()
            .Where(item => item.Id == scenario.ReservationId)
            .Select(item => new { item.VehicleId, item.CustomerId }).SingleAsync(cancellationToken);
        var inspection = await database.InspectionDrafts.AsNoTracking()
            .SingleAsync(item => item.VehicleId == purchased.VehicleId, cancellationToken);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .Where(item => item.InspectionId == inspection.Id).OrderByDescending(item => item.Revision)
            .FirstAsync(cancellationToken);
        var report = await inspectionReports.ReadAsync(purchased.CustomerId, inspection.Id, signed.Revision,
            cancellationToken);
        var expected = status == SalesOrderStatus.Delivered
            ? InspectionAssignmentStatus.Succeeded : InspectionAssignmentStatus.NotFound;
        if (report.Status != expected)
        {
            return new(label, "purchased inspection report scope changed; preserved", true);
        }

        return new(label, sale.Status == scenario.Target ? "matches" : "created", false);
    }

    private async Task<bool> CorrectDocumentAsync(bool seed, DevelopmentSaleScenario scenario,
        CancellationToken cancellationToken)
    {
        var original = await database.SaleDocuments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.SalesOrderId == scenario.ReservationId && item.Kind == SaleDocumentKind.PurchaseOrder &&
            item.CorrectionOperationId == null, cancellationToken);
        if (original is null)
        {
            return false;
        }

        var request = new SaleDocumentCorrectionRequest(scenario.OperationId("correct-address"), 7, original.Id,
            new(original.Contents().Review.BuyerName, scenario.CorrectedBuyerAddress!, "Correct the buyer address spelling"));
        if (seed)
        {
            return (await corrections.ReissueAsync(scenario.ManagerId, scenario.ReservationId, request,
                cancellationToken)).Status == SaleResultStatus.Succeeded;
        }

        var existing = await database.SaleDocumentCorrections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
        return existing is not null && existing.ActorId == scenario.ManagerId &&
            existing.OriginalDocumentId == original.Id && existing.ExpectedVersion == request.ExpectedVersion &&
            existing.BuyerName == request.Correction.BuyerName && existing.BuyerAddress == request.Correction.BuyerAddress &&
            existing.Reason == request.Correction.Reason && await database.SaleDocuments.AnyAsync(item =>
                item.CorrectionOperationId == request.OperationId, cancellationToken);
    }

    // Each step names the version it starts from; a replayed step succeeds with its original receipt.
    private async Task<bool> DriveAsync(DevelopmentSaleScenario scenario, CancellationToken cancellationToken)
    {
        var id = scenario.ReservationId;
        var agent = scenario.AgentId;
        var steps = new List<Func<Task<SaleCommandResult>>>
        {
            () => handling.ClaimAsync(agent, id, new(scenario.OperationId("claim"), 1), cancellationToken),
            () => terms.SaveTermsAsync(agent, id,
                new(scenario.OperationId("terms"), 2, null, scenario.Discount, null, 0m), cancellationToken)
        };
        if (scenario.ReleasedFirst)
        {
            steps.Add(() => handling.ReleaseAsync(agent, id, new(scenario.OperationId("release"), 3),
                cancellationToken));
            steps.Add(() => handling.AssignAsync(scenario.ManagerId, id,
                new(scenario.OperationId("assign"), 4, agent), cancellationToken));
            steps.Add(() => terms.SaveTermsAsync(agent, id,
                new(scenario.OperationId("terms-again"), 5, null, scenario.Discount, null, 0m), cancellationToken));
        }

        if (scenario.Target is SalesOrderStatus.PendingApproval or SalesOrderStatus.Approved or SalesOrderStatus.Settled or SalesOrderStatus.Delivered)
        {
            steps.Add(() => terms.SubmitAsync(agent, id, new(scenario.OperationId("submit"), 3), cancellationToken));
        }

        if (scenario.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered)
        {
            steps.Add(() => terms.ApproveAsync(scenario.ManagerId, id,
                new(scenario.OperationId("approve"), 4), cancellationToken));
        }

        foreach (var step in steps)
        {
            if ((await step()).Status != SaleResultStatus.Succeeded)
            {
                return false;
            }
        }

        if (scenario.Target is SalesOrderStatus.Settled or SalesOrderStatus.Delivered)
        {
            var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
            var profile = await database.CustomerProfiles.AsNoTracking()
                .SingleAsync(item => item.UserId == sale.CustomerId, cancellationToken);
            var vehicle = await database.Vehicles.AsNoTracking()
                .SingleAsync(item => item.Id == sale.VehicleId, cancellationToken);
            var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress,
                SaleReviewedVehicle.Of(vehicle));
            var payment = new SalePaymentEvidence(SalePaymentMethod.BankTransfer, sale.BalancePayable,
                $"Transfer {scenario.Hold.Listing.Intake.RegistrationPlate}", DateOnly.FromDateTime(sale.CreatedAt.UtcDateTime),
                null, 0m, null, false, false);
            if ((await settlement.SettleAsync(agent, id, new(scenario.OperationId("settle"), 5, payment, review),
                cancellationToken)).Status != SaleResultStatus.Succeeded)
            {
                return false;
            }

            if (scenario.Target == SalesOrderStatus.Delivered)
            {
                return (await handover.DeliverAsync(agent, id, new(scenario.OperationId("handover"), 6,
                    new(review.Vehicle.MileageKilometres, 2, true, true, true)), cancellationToken)).Status ==
                    SaleResultStatus.Succeeded;
            }
        }

        return true;
    }
}
