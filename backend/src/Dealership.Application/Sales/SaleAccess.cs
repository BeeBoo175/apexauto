using Dealership.Application.Accounts;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Who may see and act on a sale. Agents work their own branch; branch managers their branch; the General Manager all.
public static class SaleAccess
{
    public static bool IsManager(SaleStaff actor, Guid branchId) =>
        actor.Role == AccountRoles.GeneralManager ||
        actor.Role == AccountRoles.BranchManager && actor.BranchId == branchId;

    public static bool IsAgent(SaleStaff actor, Guid branchId) =>
        actor.Role == AccountRoles.SalesAgent && actor.BranchId == branchId;

    // An agent sees open sales of their branch, to claim them, and the sales they currently handle.
    public static bool CanSee(SaleStaff actor, SalesOrder sale) =>
        IsManager(actor, sale.BranchId) ||
        IsAgent(actor, sale.BranchId) && (sale.Status == SalesOrderStatus.Open || sale.AgentId == actor.Id);

    public static bool IsEligibleStaff(SaleStaff? actor, bool branchActive) =>
        actor is { IsActive: true } &&
        actor.Role is AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager &&
        (actor.Role == AccountRoles.GeneralManager || actor.BranchId is not null && branchActive);

    // Who may run each command, whatever the sale's state; the commands answer Forbidden when these fail and
    // Conflict when only the state is wrong. Actions combines both for the editor, so the two cannot drift.
    public static bool MayClaim(SaleStaff actor, SalesOrder sale) => IsAgent(actor, sale.BranchId);

    public static bool MayAssign(SaleStaff actor, SalesOrder sale) => IsManager(actor, sale.BranchId);

    public static bool MayRelease(SaleStaff actor, SalesOrder sale) =>
        IsManager(actor, sale.BranchId) || IsHandlingAgent(actor, sale);

    public static bool MayKeyTerms(SaleStaff actor, SalesOrder sale) => IsHandlingAgent(actor, sale);

    public static bool MayDecide(SaleStaff actor, SalesOrder sale) => IsManager(actor, sale.BranchId);

    // Agents cancel only a sale they are still keying; managers cancel any sale.
    public static bool MayCancel(SaleStaff actor, SalesOrder sale)
    {
        var keying = IsHandlingAgent(actor, sale) && sale.Status == SalesOrderStatus.Handling;
        return IsManager(actor, sale.BranchId) || keying;
    }

    public static SaleActions Actions(SaleStaff actor, SalesOrder sale)
    {
        var handling = sale.Status == SalesOrderStatus.Handling;
        return new SaleActions(
            Claim: MayClaim(actor, sale) && sale.Status == SalesOrderStatus.Open,
            Assign: MayAssign(actor, sale) && !sale.IsClosed,
            Release: MayRelease(actor, sale) && handling,
            SaveTerms: MayKeyTerms(actor, sale) && sale.Status is SalesOrderStatus.Handling or SalesOrderStatus.Approved,
            Submit: MayKeyTerms(actor, sale) && handling,
            Decide: MayDecide(actor, sale) && sale.Status == SalesOrderStatus.PendingApproval,
            Cancel: MayCancel(actor, sale) && !sale.IsClosed && sale.Status != SalesOrderStatus.Settled);
    }

    private static bool IsHandlingAgent(SaleStaff actor, SalesOrder sale) =>
        IsAgent(actor, sale.BranchId) && sale.AgentId == actor.Id;
}
