using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// The steps every sale command shares: request shape, actor scope, replay matching and applying one domain change.
internal static class SaleCommandSteps
{
    public static bool Valid(Guid actorId, Guid saleId, Guid operationId, long expectedVersion) =>
        actorId != Guid.Empty && saleId != Guid.Empty && operationId != Guid.Empty && expectedVersion is >= 1 and < long.MaxValue;

    // The actor must be current staff in an active branch and able to see the sale; others learn nothing about it.
    public static (SaleStaff? Actor, SalesOrder? Sale, SaleResultStatus? Failure) Scope(SaleCommandContext context)
    {
        if (!SaleAccess.IsEligibleStaff(context.Actor, context.BranchActive))
        {
            return (null, null, SaleResultStatus.Forbidden);
        }

        var hidden = context.Sale is not null && !SaleAccess.CanSee(context.Actor!, context.Sale);
        if (context.Sale is null || hidden && context.Existing is null)
        {
            return (null, null, SaleResultStatus.NotFound);
        }

        return (context.Actor, context.Sale, null);
    }

    // Every sale command runs the same sequence: request shape, lock, actor scope, replay, permission, base
    // version and state, then one domain change. A command supplies only what differs.
    public static async Task<SaleCommandResult> RunAsync(ISaleCommandStore sales, SaleCommandIdentity identity,
        SalesOrderEventKind[] kinds, Func<SaleStaff, SalesOrder, bool> may,
        Func<ISaleCommandTransaction, SaleStaff, SalesOrder, Task<SaleCommandResult>> act,
        CancellationToken cancellationToken, Func<SalesOrderEvent, bool>? replayMatches = null,
        Func<SalesOrder, bool>? staleState = null)
    {
        if (!Valid(identity.ActorId, identity.SaleId, identity.OperationId, identity.ExpectedVersion))
        {
            return new(SaleResultStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(identity.ActorId, identity.SaleId, identity.OperationId,
            cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
        if (failure is not null || actor is null || sale is null)
        {
            return new(failure ?? SaleResultStatus.NotFound);
        }

        if (transaction.Context.Existing is { } existing)
        {
            if (replayMatches is not null && !replayMatches(existing))
            {
                return new(SaleResultStatus.Conflict);
            }

            return Replayed(existing, identity.SaleId, identity.ActorId, identity.ExpectedVersion, kinds);
        }

        if (!may(actor, sale))
        {
            return new(SaleResultStatus.Forbidden);
        }

        if (sale.Version != identity.ExpectedVersion || staleState?.Invoke(sale) == true)
        {
            return new(SaleResultStatus.Conflict);
        }

        if (transaction.Context.VehicleOccupied)
        {
            return new(SaleResultStatus.Unavailable);
        }

        return await act(transaction, actor, sale);
    }

    public static SaleCommandResult Replayed(SalesOrderEvent existing, Guid saleId, Guid actorId,
        long expectedVersion, params SalesOrderEventKind[] kinds)
    {
        if (!IsReplay(existing, saleId, actorId, expectedVersion, kinds))
        {
            return new(SaleResultStatus.Conflict);
        }

        return new(SaleResultStatus.Succeeded, SaleReceipt.From(existing));
    }

    // Domain refusals become conflicts (wrong state) or invalid requests (bad values) and nothing is written.
    public static async Task<SaleCommandResult> ApplyAsync(ISaleCommandTransaction transaction,
        Func<SalesOrderEvent> act, CancellationToken cancellationToken)
    {
        SalesOrderEvent entry;
        try
        {
            entry = act();
        }
        catch (InvalidOperationException)
        {
            return new(SaleResultStatus.Conflict);
        }
        catch (ArgumentException)
        {
            return new(SaleResultStatus.Invalid);
        }

        var status = await transaction.CommitAsync(entry, cancellationToken);
        if (status != SaleResultStatus.Succeeded)
        {
            return new(status);
        }

        return new(status, SaleReceipt.From(entry));
    }

    // A replay is the same operation by the same actor, of the same kind, from the same base version. A sale's
    // Version equals the Sequence of its latest event, so the replayed event sits one past the expected version.
    private static bool IsReplay(SalesOrderEvent existing, Guid saleId, Guid actorId, long expectedVersion,
        SalesOrderEventKind[] kinds) =>
        existing.SalesOrderId == saleId && existing.ActorId == actorId && kinds.Contains(existing.Kind) &&
        existing.Sequence == expectedVersion + 1;
}

// Who is doing what to which sale, from which base version; the operation ID makes a lost response replayable.
internal readonly record struct SaleCommandIdentity(Guid ActorId, Guid SaleId, Guid OperationId, long ExpectedVersion);
