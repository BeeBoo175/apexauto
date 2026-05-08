using Dealership.Application.Accounts;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Who handles a sale: an agent claims it, a manager assigns or moves it, and releasing gives it back with its time.
public sealed class SaleHandlingService(ISaleCommandStore sales)
{
    public async Task<SaleCommandResult> ClaimAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = SaleCommandSteps.Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (transaction.Context.Existing is { } existing)
        {
            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion,
                SalesOrderEventKind.Claimed);
        }

        if (!SaleAccess.MayClaim(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion || sale.Status != SalesOrderStatus.Open)
        {
            return new(SaleStatus.Conflict);
        }

        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Claim(request.OperationId, actorId, actorId, transaction.Context.Now), cancellationToken);
    }

    // Assigning an open sale and moving a handled one are the same manager decision: who handles it now.
    public async Task<SaleCommandResult> AssignAsync(Guid actorId, Guid saleId, SaleAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) || request.AgentId == Guid.Empty)
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = SaleCommandSteps.Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (transaction.Context.Existing is { } existing)
        {
            if (existing.AgentId != request.AgentId)
            {
                return new(SaleStatus.Conflict);
            }

            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion,
                SalesOrderEventKind.Assigned, SalesOrderEventKind.Reassigned);
        }

        if (!SaleAccess.MayAssign(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        var agent = await transaction.LockStaffAsync(request.AgentId, cancellationToken);
        if (agent is not { IsActive: true, Role: AccountRoles.SalesAgent } || agent.BranchId != sale.BranchId)
        {
            return new(SaleStatus.Invalid);
        }

        var now = transaction.Context.Now;
        if (sale.Status == SalesOrderStatus.Open)
        {
            return await SaleCommandSteps.ApplyAsync(transaction,
                () => sale.Claim(request.OperationId, agent.Id, actorId, now), cancellationToken);
        }

        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Reassign(request.OperationId, agent.Id, actorId, now), cancellationToken);
    }

    public async Task<SaleCommandResult> ReleaseAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = SaleCommandSteps.Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (transaction.Context.Existing is { } existing)
        {
            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion,
                SalesOrderEventKind.Released);
        }

        if (!SaleAccess.MayRelease(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Release(request.OperationId, actorId, transaction.Context.Now), cancellationToken);
    }
}
