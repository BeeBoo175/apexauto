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

    // A replay is the same operation by the same actor, of the same kind, from the same base version.
    public static bool IsReplay(SalesOrderEvent existing, Guid saleId, Guid actorId, long expectedVersion,
        params SalesOrderEventKind[] kinds) =>
        existing.SalesOrderId == saleId && existing.ActorId == actorId && kinds.Contains(existing.Kind) &&
        existing.Sequence == expectedVersion + 1;
}
