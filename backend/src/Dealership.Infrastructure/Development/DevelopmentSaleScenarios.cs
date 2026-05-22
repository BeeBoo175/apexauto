using Dealership.Application.Accounts;
using Dealership.Domain.Sales;
using System.Collections.ObjectModel;

namespace Dealership.Infrastructure.Development;

// A demo sale driven to one state through the canonical services, so the staff tabs have real examples. It works
// the open hold each branch already keeps, so the seeded catalogue keeps its accepted volume.
public sealed record DevelopmentSaleScenario(DevelopmentReservationScenario Hold, SalesOrderStatus Target,
    bool ReleasedFirst, decimal Discount, string? CorrectedBuyerAddress = null, bool ManagerApproval = false)
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
    // The first branch's sale was released and reassigned, the second is delivered after settlement and manager approval of its
    // excess discount, and the third stays open to be claimed.
    public static IReadOnlyList<DevelopmentSaleScenario> All { get; } = Create();

    private static ReadOnlyCollection<DevelopmentSaleScenario> Create()
    {
        var holds = DevelopmentReservationScenarios.All.Where(item => item.KeepActive).ToArray();
        DevelopmentReservationScenario Hold(int branch) => holds.Single(item =>
            item.Listing.Intake.BranchId == DevelopmentFixtureManifest.Branches[branch].Id);
        return Array.AsReadOnly<DevelopmentSaleScenario>(
        [
            new(Hold(0), SalesOrderStatus.Handling, true, 300m),
            new(Hold(1), SalesOrderStatus.Delivered, false, 1500m,
                "27 Jalan Meranti, Petaling Jaya, Selangor", ManagerApproval: true)
        ]);
    }
}
