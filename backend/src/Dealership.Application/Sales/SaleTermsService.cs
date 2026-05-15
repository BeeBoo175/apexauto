using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

// What a sale is agreed for and who decides it: the handling agent keys and submits terms, a manager approves or
// rejects an excess discount, and staff cancel with a reason, which refunds the deposit.
public sealed class SaleTermsService(ISaleCommandStore sales)
{
    private const decimal MaximumTermAmount = 500000m;

    public Task<SaleCommandResult> SaveTermsAsync(Guid actorId, Guid saleId, SaleTermsRequest request,
        CancellationToken cancellationToken)
    {
        // A trade-in car and a positive allowance come together or not at all.
        var tradeInPaired = request.TradeInAppointmentId is null
            ? request.TradeInAllowance == 0m
            : request.TradeInAllowance > 0m;
        if (!tradeInPaired ||
            request.WarrantyPackageId == Guid.Empty || request.TradeInAppointmentId == Guid.Empty ||
            !ValidAmount(request.NegotiatedDiscount) || !ValidAmount(request.TradeInAllowance))
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.TermsSaved], SaleAccess.MayKeyTerms,
            async (transaction, actor, sale) =>
            {
                var settings = await transaction.LockSettingsAsync(cancellationToken);
                if (settings is null)
                {
                    return new SaleCommandResult(SaleResultStatus.Unavailable);
                }

                SaleWarranty? warranty = null;
                if (request.WarrantyPackageId is { } packageId)
                {
                    warranty = await transaction.ResolveWarrantyAsync(packageId, cancellationToken);
                    if (warranty is null)
                    {
                        return new SaleCommandResult(SaleResultStatus.Invalid);
                    }
                }

                SaleTradeIn? tradeIn = null;
                if (request.TradeInAppointmentId is { } appointmentId)
                {
                    var choice = await transaction.ResolveTradeInAsync(appointmentId, cancellationToken);
                    // The allowance can never exceed the customer's own confirmed valuation.
                    if (choice is null || request.TradeInAllowance > choice.Maximum)
                    {
                        return new SaleCommandResult(SaleResultStatus.Invalid);
                    }

                    if (choice.TakenByAnotherSale)
                    {
                        return new SaleCommandResult(SaleResultStatus.Conflict);
                    }

                    tradeIn = new SaleTradeIn(appointmentId, choice.Label, request.TradeInAllowance);
                }

                var input = new SaleTermsInput(warranty, request.NegotiatedDiscount, tradeIn);
                return await SaleCommandSteps.ApplyAsync(transaction,
                    () => sale.SaveTerms(request.OperationId, input, settings, actorId, transaction.Context.Now),
                    cancellationToken);
            },
            cancellationToken, replayMatches: existing =>
            {
                var snapshot = existing.Snapshot;
                return snapshot.WarrantyPackageId == request.WarrantyPackageId &&
                    snapshot.NegotiatedDiscount == request.NegotiatedDiscount &&
                    snapshot.TradeInAppointmentId == request.TradeInAppointmentId &&
                    snapshot.TradeInAllowance == request.TradeInAllowance;
            });
    }

    public Task<SaleCommandResult> SubmitAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Submitted], SaleAccess.MayKeyTerms,
            async (transaction, actor, sale) =>
            {
                var settings = await transaction.LockSettingsAsync(cancellationToken);
                if (settings is null)
                {
                    return new SaleCommandResult(SaleResultStatus.Unavailable);
                }

                var now = transaction.Context.Now;
                var number = sale.OrderNumber ?? await transaction.NextOrderNumberAsync(now.Year, cancellationToken);
                return await SaleCommandSteps.ApplyAsync(transaction,
                    () => sale.Submit(request.OperationId, settings, number, actorId, now), cancellationToken);
            },
            cancellationToken, staleState: sale => sale.Status != SalesOrderStatus.Handling);
    }

    public Task<SaleCommandResult> ApproveAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        return DecideAsync(actorId, saleId, new SaleReasonRequest(request.OperationId, request.ExpectedVersion,
            null), approve: true, cancellationToken);
    }

    public Task<SaleCommandResult> RejectAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!SalesOrder.ValidReason(request.Reason))
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return DecideAsync(actorId, saleId, request, approve: false, cancellationToken);
    }

    public Task<SaleCommandResult> CancelAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!SalesOrder.ValidReason(request.Reason))
        {
            return Task.FromResult(new SaleCommandResult(SaleResultStatus.Invalid));
        }

        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [SalesOrderEventKind.Cancelled], SaleAccess.MayCancel,
            async (transaction, actor, sale) =>
            {
                var kind = sale.Status == SalesOrderStatus.Open
                    ? ReservationReleaseKind.StaffCancelled
                    : ReservationReleaseKind.DealCancelled;
                var releaseId = await transaction.ReleaseReservationAsync(kind, actorId, request.Reason!,
                    cancellationToken);
                if (releaseId is null)
                {
                    return new SaleCommandResult(SaleResultStatus.Conflict);
                }

                return await SaleCommandSteps.ApplyAsync(transaction,
                    () => sale.Cancel(request.OperationId, actorId, request.Reason!, releaseId.Value,
                        transaction.Context.Now), cancellationToken);
            },
            cancellationToken, replayMatches: existing => existing.Reason == request.Reason,
            staleState: sale => sale.IsClosed);
    }

    private Task<SaleCommandResult> DecideAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        bool approve, CancellationToken cancellationToken)
    {
        var kind = approve ? SalesOrderEventKind.Approved : SalesOrderEventKind.Rejected;
        return SaleCommandSteps.RunAsync(sales, new(actorId, saleId, request.OperationId, request.ExpectedVersion),
            [kind], SaleAccess.MayDecide,
            (transaction, actor, sale) =>
            {
                var now = transaction.Context.Now;
                return approve
                    ? SaleCommandSteps.ApplyAsync(transaction,
                        () => sale.Approve(request.OperationId, actorId, now), cancellationToken)
                    : SaleCommandSteps.ApplyAsync(transaction,
                        () => sale.Reject(request.OperationId, actorId, request.Reason!, now), cancellationToken);
            },
            cancellationToken, replayMatches: existing => existing.Reason == request.Reason);
    }

    private static bool ValidAmount(decimal amount) => amount is >= 0m and <= MaximumTermAmount &&
        decimal.Round(amount, 2) == amount;
}
