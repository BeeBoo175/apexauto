using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Settlement uses the approved amounts under the same locks and replay contract as the other sale commands.
public sealed class SaleSettlementService(ISaleCommandStore sales)
{
    public Task<SaleCommandResult> SettleAsync(Guid actorId, Guid saleId, SaleSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Payment is null || request.Review is null)
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Settled], SaleAccess.MayKeyTerms,
            async (transaction, actor, sale) =>
            {
                SaleSettlementReview review;
                try
                {
                    review = request.Review.Normalize();
                }
                catch (ArgumentException)
                {
                    return new SaleCommandResult(SaleResultStatus.Invalid);
                }

                if (!await transaction.ReviewSettlementAsync(review, cancellationToken))
                {
                    return new SaleCommandResult(SaleResultStatus.Conflict);
                }

                var acquiredVehicleId = await transaction.LockTradeInVehicleAsync(cancellationToken);
                if (sale.TradeInAppointmentId is not null && acquiredVehicleId is null)
                {
                    return new SaleCommandResult(SaleResultStatus.Conflict);
                }

                return await SaleCommandSteps.ApplyAsync(transaction,
                    () =>
                    {
                        var entry = sale.Settle(request.OperationId, request.Payment, acquiredVehicleId, actor.Id,
                            transaction.Context.Now);
                        entry.CaptureSettlementReview(review);
                        return entry;
                    }, cancellationToken);
            },
            cancellationToken, replayMatches: existing => Matches(existing, request),
            staleState: sale => sale.Status != SalesOrderStatus.Approved);
    }

    private static bool Matches(SalesOrderEvent existing, SaleSettlementRequest request)
    {
        try
        {
            return existing.Snapshot.PaymentEvidence == request.Payment.Normalize(existing.Snapshot.BalancePayable) &&
                existing.SettlementReview == request.Review.Normalize();
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
