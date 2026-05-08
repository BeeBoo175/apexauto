using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// The steps every sale command shares: request shape, actor scope, replay matching and applying one domain change.
internal static class SaleCommandSteps
{
    public static bool Valid(Guid actorId, Guid saleId, Guid operationId, long expectedVersion) =>
        actorId != Guid.Empty && saleId != Guid.Empty && operationId != Guid.Empty && expectedVersion is >= 1 and < long.MaxValue;

    // The actor must be current staff in an active branch and able to see the sale; others learn nothing about it.
    public static (SaleStaff? Actor, SalesOrder? Sale, SaleStatus? Failure) Scope(SaleCommandContext context)
    {
        if (!SaleAccess.IsEligibleStaff(context.Actor, context.BranchActive))
        {
            return (null, null, SaleStatus.Forbidden);
        }

        var hidden = context.Sale is not null && !SaleAccess.CanSee(context.Actor!, context.Sale);
        if (context.Sale is null || hidden && context.Existing is null)
        {
            return (null, null, SaleStatus.NotFound);
        }

        return (context.Actor, context.Sale, null);
    }

    public static SaleCommandResult Replayed(SalesOrderEvent existing, Guid saleId, Guid actorId,
        long expectedVersion, params SalesOrderEventKind[] kinds)
    {
        if (!IsReplay(existing, saleId, actorId, expectedVersion, kinds))
        {
            return new(SaleStatus.Conflict);
        }

        return new(SaleStatus.Succeeded, SaleReceipt.From(existing));
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
            return new(SaleStatus.Conflict);
        }
        catch (ArgumentException)
        {
            return new(SaleStatus.Invalid);
        }

        var status = await transaction.CommitAsync(entry, cancellationToken);
        if (status != SaleStatus.Succeeded)
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
