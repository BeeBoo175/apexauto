using Dealership.Application.Accounts;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Who handles a sale: an agent claims it, a manager assigns or moves it, and releasing gives it back with its time.
public sealed class SaleHandlingService(ISaleCommandStore sales)
{
    public Task<SaleCommandResult> ClaimAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Claimed], SaleAccess.MayClaim,
            (transaction, actor, sale) => SaleCommandSteps.ApplyAsync(transaction,
                () => sale.Claim(request.OperationId, actorId, actorId, transaction.Context.Now), cancellationToken),
            cancellationToken, staleState: sale => sale.Status != SalesOrderStatus.Open);
    }

    // Assigning an open sale and moving a handled one are the same manager decision: who handles it now.
    public Task<SaleCommandResult> AssignAsync(Guid actorId, Guid saleId, SaleAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AgentId == Guid.Empty)
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Assigned, SalesOrderEventKind.Reassigned], SaleAccess.MayAssign,
            async (transaction, actor, sale) =>
            {
                var agent = await transaction.LockStaffAsync(request.AgentId, cancellationToken);
                if (agent is not { IsActive: true, Role: AccountRoles.SalesAgent } || agent.BranchId != sale.BranchId)
                {
                    return new SaleCommandResult(SaleResultStatus.Invalid);
                }

                var now = transaction.Context.Now;
                if (sale.Status == SalesOrderStatus.Open)
                {
                    return await SaleCommandSteps.ApplyAsync(transaction,
                        () => sale.Claim(request.OperationId, agent.Id, actorId, now), cancellationToken);
                }

                return await SaleCommandSteps.ApplyAsync(transaction,
                    () => sale.Reassign(request.OperationId, agent.Id, actorId, now), cancellationToken);
            },
            cancellationToken, replayMatches: existing => existing.AgentId == request.AgentId);
    }

    public Task<SaleCommandResult> ReleaseAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Released], SaleAccess.MayRelease,
            (transaction, actor, sale) => SaleCommandSteps.ApplyAsync(transaction,
                () => sale.Release(request.OperationId, actorId, transaction.Context.Now), cancellationToken),
            cancellationToken);
    }
}
