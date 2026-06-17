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
    public const string RejectionReason = "Requested discount exceeds the branch limit; renegotiate the terms.";

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentSaleScenarios.All)
        {
            reports.Add(await RunOneAsync(seed, scenario, cancellationToken));
        }

        return reports;
    }

    public async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentSaleScenario scenario,
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

        // Replay canonical steps if a claim committed before its terms save; foreign changes still conflict.
        if (seed && (sale.Status != scenario.Target || sale.Version < scenario.FinalVersion) &&
            !await DriveAsync(scenario, cancellationToken))
        {
            return new(label, "sale changed elsewhere; preserved", true);
        }

        var current = await database.SalesOrders.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ReservationId, cancellationToken);
        var status = current.Status;
        if (status == scenario.Target && current.Version != scenario.FinalVersion)
        {
            return new(label, "sale version changed elsewhere; preserved", true);
        }

        if (status is SalesOrderStatus.Settled or SalesOrderStatus.Delivered &&
            (current.PaymentEvidence?.Method != scenario.PaymentMethod ||
             current.TradeInAppointmentId != scenario.TradeInAppointmentId))
        {
            return new(label, "settlement evidence changed elsewhere; preserved", true);
        }

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

        var inspection = await database.InspectionDrafts.AsNoTracking()
            .SingleAsync(item => item.VehicleId == current.VehicleId, cancellationToken);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .Where(item => item.InspectionId == inspection.Id).OrderByDescending(item => item.Revision)
            .FirstAsync(cancellationToken);
        var report = await inspectionReports.ReadAsync(current.CustomerId, inspection.Id, signed.Revision,
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

        var request = new SaleDocumentCorrectionRequest(scenario.OperationId("correct-address"), scenario.FinalVersion, original.Id,
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
        if (scenario.Target == SalesOrderStatus.Open)
        {
            return true;
        }

        var id = scenario.ReservationId;
        var agent = scenario.AgentId;
        var request = await TermsAsync(database, scenario, cancellationToken);
        var steps = new List<Func<Task<SaleCommandResult>>>
        {
            () => handling.ClaimAsync(agent, id, new(scenario.OperationId("claim"), 1), cancellationToken),
            () => terms.SaveTermsAsync(agent, id,
                request, cancellationToken)
        };
        if (scenario.ReleasedFirst)
        {
            steps.Add(() => handling.ReleaseAsync(agent, id, new(scenario.OperationId("release"), 3),
                cancellationToken));
            steps.Add(() => handling.AssignAsync(scenario.ManagerId, id,
                new(scenario.OperationId("assign"), 4, agent), cancellationToken));
            steps.Add(() => terms.SaveTermsAsync(agent, id,
                request with { OperationId = scenario.OperationId("terms-again"), ExpectedVersion = 5 }, cancellationToken));
        }

        if (scenario.Target is SalesOrderStatus.PendingApproval or SalesOrderStatus.Approved or SalesOrderStatus.Settled or SalesOrderStatus.Delivered ||
            scenario.RejectedOnce)
        {
            steps.Add(() => terms.SubmitAsync(agent, id,
                new(scenario.OperationId("submit"), scenario.TermsVersion), cancellationToken));
        }

        if (scenario.RejectedOnce)
        {
            steps.Add(() => terms.RejectAsync(scenario.ManagerId, id, new(scenario.OperationId("reject"),
                scenario.SubmittedVersion, RejectionReason),
                cancellationToken));
        }

        if (scenario.ManagerApproval && scenario.Target is
            SalesOrderStatus.Approved or SalesOrderStatus.Settled or SalesOrderStatus.Delivered)
        {
            steps.Add(() => terms.ApproveAsync(scenario.ManagerId, id,
                new(scenario.OperationId("approve"), scenario.SubmittedVersion), cancellationToken));
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
            var review = await ReviewAsync(database, sale, cancellationToken);
            var payment = Payment(scenario, sale.BalancePayable, sale.CreatedAt);
            if ((await settlement.SettleAsync(agent, id,
                new(scenario.OperationId("settle"), scenario.ApprovedVersion, payment, review),
                cancellationToken)).Status != SaleResultStatus.Succeeded)
            {
                return false;
            }

            if (scenario.Target == SalesOrderStatus.Delivered)
            {
                return (await handover.DeliverAsync(agent, id, new(scenario.OperationId("handover"), scenario.SettledVersion,
                    new(review.Vehicle.MileageKilometres, 2, true, true, true)), cancellationToken)).Status ==
                    SaleResultStatus.Succeeded;
            }
        }

        return true;
    }

    public static async Task<SaleSettlementReview> ReviewAsync(DealershipDbContext database, SalesOrder sale,
        CancellationToken cancellationToken)
    {
        var profile = await database.CustomerProfiles.AsNoTracking()
            .SingleAsync(item => item.UserId == sale.CustomerId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == sale.VehicleId, cancellationToken);
        return new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress,
            SaleReviewedVehicle.Of(vehicle));
    }

    public static async Task<SaleTermsRequest> TermsAsync(DealershipDbContext database,
        DevelopmentSaleScenario scenario, CancellationToken cancellationToken)
    {
        if (scenario.PaymentMethod != SalePaymentMethod.ZeroBalance)
        {
            return new(scenario.OperationId("terms"), 2, scenario.WarrantyPackageId, scenario.Discount,
                scenario.TradeInAppointmentId, 0m);
        }

        var hold = await database.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ReservationId, cancellationToken);
        var settings = await database.PurchaseSettings.AsNoTracking().SingleAsync(cancellationToken);
        var price = SalePricing.Calculate(new(hold.Price.VehiclePrice, hold.Price.RoadTax,
            settings.ProcessingFee, settings.SstRate, hold.Price.PromoDiscount, scenario.Discount, 0m, hold.Deposit, 0m));
        return new(scenario.OperationId("terms"), 2, null, scenario.Discount, scenario.TradeInAppointmentId,
            price.BalancePayable);
    }

    public static SalePaymentEvidence Payment(DevelopmentSaleScenario scenario, decimal balance, DateTimeOffset createdAt)
    {
        var reference = scenario.Hold.Listing.Intake.RegistrationPlate;
        return scenario.PaymentMethod switch
        {
            SalePaymentMethod.ZeroBalance => new(SalePaymentMethod.ZeroBalance, 0m, null, null,
                null, 0m, null, false, true),
            SalePaymentMethod.HirePurchase => new(SalePaymentMethod.HirePurchase, 500m, null, null,
                "Maybank", balance - 500m, $"LOU {reference}", true, false),
            SalePaymentMethod.BankTransfer => new(SalePaymentMethod.BankTransfer, balance, $"Transfer {reference}",
                DateOnly.FromDateTime(createdAt.UtcDateTime), null, 0m, null, false, false),
            _ => new(scenario.PaymentMethod, balance, $"Receipt {reference}", DateOnly.FromDateTime(createdAt.UtcDateTime),
                null, 0m, null, false, false)
        };
    }
}
