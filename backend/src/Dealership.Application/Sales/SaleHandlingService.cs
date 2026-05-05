using Dealership.Application.Accounts;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Who handles a sale: an agent claims it, a manager assigns or moves it, and releasing gives it back with its time.
public sealed class SaleHandlingService(ISaleCommandStore sales)
{
    public async Task<SaleCommandResult> ClaimAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!Valid(actorId, saleId, request.OperationId, request.ExpectedVersion))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (!SaleAccess.IsAgent(actor!, sale!.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (transaction.Context.Existing is { } existing)
        {
            return Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.Claimed);
        }

        if (sale.Version != request.ExpectedVersion || sale.Status != SalesOrderStatus.Open)
        {
            return new(SaleStatus.Conflict);
        }

        return await ApplyAsync(transaction, () => sale.Claim(request.OperationId, actorId, actorId,
            transaction.Context.Now), cancellationToken);
    }

    // Assigning an open sale and moving a handled one are the same manager decision: who handles it now.
    public async Task<SaleCommandResult> AssignAsync(Guid actorId, Guid saleId, SaleAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) || request.AgentId == Guid.Empty)
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (!SaleAccess.IsManager(actor!, sale!.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (transaction.Context.Existing is { } existing)
        {
            var matches = existing.AgentId == request.AgentId;
            return matches
                ? Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.Assigned,
                    SalesOrderEventKind.Reassigned)
                : new(SaleStatus.Conflict);
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
        return sale.Status == SalesOrderStatus.Open
            ? await ApplyAsync(transaction, () => sale.Claim(request.OperationId, agent.Id, actorId, now),
                cancellationToken)
            : await ApplyAsync(transaction, () => sale.Reassign(request.OperationId, agent.Id, actorId, now),
                cancellationToken);
    }

    public async Task<SaleCommandResult> ReleaseAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (!Valid(actorId, saleId, request.OperationId, request.ExpectedVersion))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        var ownClaim = SaleAccess.IsAgent(actor!, sale!.BranchId) && sale.AgentId == actorId;
        if (transaction.Context.Existing is { } existing)
        {
            return Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.Released);
        }

        if (!ownClaim && !SaleAccess.IsManager(actor!, sale.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        return await ApplyAsync(transaction, () => sale.Release(request.OperationId, actorId, transaction.Context.Now),
            cancellationToken);
    }

    internal static bool Valid(Guid actorId, Guid saleId, Guid operationId, long expectedVersion) =>
        actorId != Guid.Empty && saleId != Guid.Empty && operationId != Guid.Empty && expectedVersion is >= 1 and < long.MaxValue;

    // The actor must be current staff in an active branch and able to see the sale; others learn nothing about it.
    internal static (SaleStaff? Actor, SalesOrder? Sale, SaleStatus? Failure) Scope(SaleCommandContext context)
    {
        if (!SaleAccess.IsEligibleStaff(context.Actor, context.BranchActive))
        {
            return (null, null, SaleStatus.Forbidden);
        }

        if (context.Sale is null || !SaleAccess.CanSee(context.Actor!, context.Sale) && context.Existing is null)
        {
            return (null, null, SaleStatus.NotFound);
        }

        return (context.Actor, context.Sale, null);
    }

    internal static SaleCommandResult Replayed(SalesOrderEvent existing, Guid saleId, Guid actorId,
        long expectedVersion, params SalesOrderEventKind[] kinds) =>
        SaleAccess.IsReplay(existing, saleId, actorId, expectedVersion, kinds)
            ? new(SaleStatus.Succeeded, SaleReceipt.From(existing))
            : new(SaleStatus.Conflict);

    // Domain refusals become conflicts (wrong state) or invalid requests (bad values) and nothing is written.
    internal static async Task<SaleCommandResult> ApplyAsync(ISaleCommandTransaction transaction,
        Func<SalesOrderEvent> act, CancellationToken cancellationToken)
    {
        SalesOrderEvent entry;
        try
        {
            entry = act();
        }
        catch (InvalidOperationException)
        {
            return new(SaleStatus.Conflict);
        }
        catch (ArgumentException)
        {
            return new(SaleStatus.Invalid);
        }

        var status = await transaction.CommitAsync(entry, cancellationToken);
        return status == SaleStatus.Succeeded ? new(status, SaleReceipt.From(entry)) : new(status);
    }
}
