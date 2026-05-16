using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

public sealed class SaleHandoverService(ISaleCommandStore sales)
{
    public Task<SaleCommandResult> DeliverAsync(Guid actorId, Guid saleId, SaleHandoverRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Evidence is null)
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Delivered], SaleAccess.MayKeyTerms,
            async (transaction, actor, sale) =>
            {
                var mileage = await transaction.ReadVehicleMileageAsync(cancellationToken);
                return await SaleCommandSteps.ApplyAsync(transaction,
                    () => sale.Deliver(request.OperationId, request.Evidence, mileage, actor.Id,
                        transaction.Context.Now), cancellationToken);
            }, cancellationToken, replayMatches: existing => existing.Snapshot.HandoverEvidence == request.Evidence,
            staleState: sale => sale.Status != SalesOrderStatus.Settled);
    }
}
