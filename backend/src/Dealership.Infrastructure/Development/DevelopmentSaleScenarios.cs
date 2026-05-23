using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using System.Collections.ObjectModel;

namespace Dealership.Infrastructure.Development;

// Finite sale paths use canonical commands and retain the reservation's stable operation identities.
public sealed record DevelopmentSaleScenario(DevelopmentReservationScenario Hold, SalesOrderStatus Target,
    bool ReleasedFirst, decimal Discount, string? CorrectedBuyerAddress = null, bool ManagerApproval = false,
    SalePaymentMethod PaymentMethod = SalePaymentMethod.BankTransfer, Guid? TradeInAppointmentId = null)
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
        SalesOrderStatus.Handling => TermsVersion,
        SalesOrderStatus.PendingApproval => SubmittedVersion,
        SalesOrderStatus.Approved => ApprovedVersion,
        SalesOrderStatus.Settled => SettledVersion,
        SalesOrderStatus.Delivered => SettledVersion + 1,
        _ => throw new InvalidOperationException("The sale fixture has no command path for this target.")
    };

    public Guid AgentId => DevelopmentFixtureManifest.Staff.Single(item =>
        item.Role == AccountRoles.SalesAgent && item.BranchId == Hold.Listing.Intake.BranchId).Id;

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
            new(Extra(74), SalesOrderStatus.Approved, false, 500m)
        ]);
    }
}
