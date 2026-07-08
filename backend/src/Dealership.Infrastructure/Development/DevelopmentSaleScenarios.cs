using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using System.Collections.ObjectModel;

namespace Dealership.Infrastructure.Development;

// Finite sale paths use canonical commands and retain the reservation's stable operation identities. HeldStep places
// a dated sale's hold, in tenths of the gap between its listing's publication and the snapshot, so completed sales
// of vehicles that carry no other hold spread across the weeks of history.
public sealed record DevelopmentSaleScenario(DevelopmentReservationScenario Hold, SalesOrderStatus Target,
    bool ReleasedFirst, decimal Discount, string? CorrectedBuyerAddress = null, bool ManagerApproval = false,
    SalePaymentMethod PaymentMethod = SalePaymentMethod.BankTransfer, Guid? TradeInAppointmentId = null,
    Guid? WarrantyPackageId = null, bool RejectedOnce = false, int HeldStep = 5)
{
    public Guid ReservationId => Hold.Id;

    public Guid OperationId(string step) => Hold.Listing.OperationId($"sale-{step}");

    public long TermsVersion => ReleasedFirst ? 6 : 3;

    public long SubmittedVersion => TermsVersion + 1;

    public long ApprovedVersion => SubmittedVersion + (ManagerApproval ? 1 : 0);

    public long SettledVersion => ApprovedVersion + 1;

    public long FinalVersion => Target switch
    {
        SalesOrderStatus.Open => 1,
        // A rejected discount returns the sale to handling two events after its terms.
        SalesOrderStatus.Handling => TermsVersion + (RejectedOnce ? 2 : 0),
        SalesOrderStatus.PendingApproval => SubmittedVersion,
        SalesOrderStatus.Approved => ApprovedVersion,
        SalesOrderStatus.Settled => SettledVersion,
        SalesOrderStatus.Delivered => SettledVersion + 1,
        _ => throw new InvalidOperationException("The sale fixture has no command path for this target.")
    };

    public Guid AgentId => DevelopmentFixtureManifest.AgentFor(Hold.Listing.Intake.BranchId, ReservationId).Id;

    public Guid ManagerId => DevelopmentFixtureManifest.Staff.Single(item =>
        item.Role == AccountRoles.BranchManager && item.BranchId == Hold.Listing.Intake.BranchId).Id;
}

public static class DevelopmentSaleScenarios
{
    public static IReadOnlyList<DevelopmentSaleScenario> All { get; } = Create();

    private static ReadOnlyCollection<DevelopmentSaleScenario> Create()
    {
        var holds = DevelopmentReservationScenarios.All.Where(item => item.KeepActive).ToArray();
        DevelopmentReservationScenario Hold(int branch) => holds.Single(item =>
            item.Index == 51 && item.Listing.Intake.BranchId == DevelopmentFixtureManifest.Branches[branch].Id);
        DevelopmentReservationScenario Extra(int index) => holds.Single(item => item.Index == index);
        var tradeIn = DevelopmentInspectionScenarios.All.First(item =>
            item.Stage == DevelopmentInspectionStage.Draft &&
            item.Intake.RegistrationKey == Extra(72).RegistrationKey);
        return Array.AsReadOnly<DevelopmentSaleScenario>(
        [
            new(Hold(0), SalesOrderStatus.Handling, true, 300m),
            new(Hold(1), SalesOrderStatus.Delivered, false, 1500m,
                "27 Jalan Meranti, Petaling Jaya, Selangor", ManagerApproval: true),
            new(Hold(2), SalesOrderStatus.Open, false, 0m),
            new(Extra(70), SalesOrderStatus.Settled, false, 300m, PaymentMethod: SalePaymentMethod.Cash),
            new(Extra(71), SalesOrderStatus.Delivered, false, 500m, PaymentMethod: SalePaymentMethod.HirePurchase),
            new(Extra(72), SalesOrderStatus.Settled, false, 0m,
                PaymentMethod: SalePaymentMethod.ZeroBalance, TradeInAppointmentId: tradeIn.Id),
            new(Extra(73), SalesOrderStatus.PendingApproval, false, 1500m),
            new(Extra(74), SalesOrderStatus.Approved, false, 500m),
            new(Extra(75), SalesOrderStatus.Approved, false, 0m,
                WarrantyPackageId: DevelopmentPurchaseFixtures.Warranties[1].Id),
            new(Extra(76), SalesOrderStatus.Handling, false, 1500m, RejectedOnce: true),
            .. CompletedHistory()
        ]);
    }

    // Completed purchases of catalogue vehicles that carry no other hold, per branch, so deliveries, delivered value
    // and delivery time have a shape across the weeks and agents. One at each branch stays settled and awaits handover.
    private static IEnumerable<DevelopmentSaleScenario> CompletedHistory()
    {
        SalePaymentMethod[] methods =
        [
            SalePaymentMethod.BankTransfer, SalePaymentMethod.HirePurchase, SalePaymentMethod.Cash
        ];
        decimal[] discounts = [100m, 300m, 500m];
        // Each vehicle's test drives in history end before its purchase, so every purchase is in the later half.
        int[][] steps = [[5, 7, 9], [6, 8, 9], [5, 6, 8]];
        for (var branch = 0; branch < DevelopmentFixtureManifest.Branches.Count; branch++)
        {
            for (var slot = 0; slot < DevelopmentReservationScenarios.CompletedPerBranch; slot++)
            {
                var hold = DevelopmentReservationScenarios.All.Single(item =>
                    item.Index == DevelopmentReservationScenarios.FirstCompletedIndex + branch *
                    DevelopmentReservationScenarios.CompletedPerBranch + slot);
                var target = slot == DevelopmentReservationScenarios.CompletedPerBranch - 1
                    ? SalesOrderStatus.Settled
                    : SalesOrderStatus.Delivered;
                yield return new(hold, target, false, discounts[slot], PaymentMethod: methods[slot],
                    HeldStep: steps[branch][slot]);
            }
        }
    }
}
