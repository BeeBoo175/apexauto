using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

// What a sale is agreed for and who decides it: the handling agent keys and submits terms, a manager approves or
// rejects an excess discount, and staff cancel with a reason, which refunds the deposit.
public sealed class SaleTermsService(ISaleCommandStore sales)
{
    private const decimal MaximumTermAmount = 500000m;

    public async Task<SaleCommandResult> SaveTermsAsync(Guid actorId, Guid saleId, SaleTermsRequest request,
        CancellationToken cancellationToken)
    {
        // A trade-in car and a positive allowance come together or not at all.
        var tradeInPaired = request.TradeInAppointmentId is null
            ? request.TradeInAllowance == 0m
            : request.TradeInAllowance > 0m;
        var validRequest = SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion);
        if (!validRequest || !tradeInPaired ||
            request.WarrantyPackageId == Guid.Empty || request.TradeInAppointmentId == Guid.Empty ||
            !ValidAmount(request.NegotiatedDiscount) || !ValidAmount(request.TradeInAllowance))
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
            var snapshot = existing.Snapshot;
            var matches = snapshot.WarrantyPackageId == request.WarrantyPackageId &&
                snapshot.NegotiatedDiscount == request.NegotiatedDiscount &&
                snapshot.TradeInAppointmentId == request.TradeInAppointmentId &&
                snapshot.TradeInAllowance == request.TradeInAllowance;
            if (!matches)
            {
                return new(SaleStatus.Conflict);
            }

            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion,
                SalesOrderEventKind.TermsSaved);
        }

        if (!SaleAccess.MayKeyTerms(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale!.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        var settings = await transaction.LockSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return new(SaleStatus.Unavailable);
        }

        SaleWarranty? warranty = null;
        if (request.WarrantyPackageId is { } packageId)
        {
            warranty = await transaction.ResolveWarrantyAsync(packageId, cancellationToken);
            if (warranty is null)
            {
                return new(SaleStatus.Invalid);
            }
        }

        SaleTradeIn? tradeIn = null;
        if (request.TradeInAppointmentId is { } appointmentId)
        {
            var choice = await transaction.ResolveTradeInAsync(appointmentId, cancellationToken);
            // The allowance can never exceed the customer's own confirmed valuation.
            if (choice is null || request.TradeInAllowance > choice.Maximum)
            {
                return new(SaleStatus.Invalid);
            }

            if (choice.TakenByAnotherSale)
            {
                return new(SaleStatus.Conflict);
            }

            tradeIn = new SaleTradeIn(appointmentId, choice.Label, request.TradeInAllowance);
        }

        var terms = new SaleTermsInput(warranty, request.NegotiatedDiscount, tradeIn);
        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.SaveTerms(request.OperationId, terms, settings, actorId, transaction.Context.Now),
            cancellationToken);
    }

    public async Task<SaleCommandResult> SubmitAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
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
                SalesOrderEventKind.Submitted);
        }

        if (!SaleAccess.MayKeyTerms(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale!.Version != request.ExpectedVersion || sale.Status != SalesOrderStatus.Handling)
        {
            return new(SaleStatus.Conflict);
        }

        var settings = await transaction.LockSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return new(SaleStatus.Unavailable);
        }

        var now = transaction.Context.Now;
        var number = sale.OrderNumber ?? await transaction.NextOrderNumberAsync(now.Year, cancellationToken);
        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Submit(request.OperationId, settings, number, actorId, now), cancellationToken);
    }

    public async Task<SaleCommandResult> ApproveAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
        CancellationToken cancellationToken)
    {
        return await DecideAsync(actorId, saleId, new SaleReasonRequest(request.OperationId, request.ExpectedVersion,
            null), approve: true, cancellationToken);
    }

    public async Task<SaleCommandResult> RejectAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!SalesOrder.ValidReason(request.Reason))
        {
            return new(SaleStatus.Invalid);
        }

        return await DecideAsync(actorId, saleId, request, approve: false, cancellationToken);
    }

    public async Task<SaleCommandResult> CancelAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!SaleCommandSteps.Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) ||
            !SalesOrder.ValidReason(request.Reason))
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
            if (existing.Reason != request.Reason)
            {
                return new(SaleStatus.Conflict);
            }

            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion,
                SalesOrderEventKind.Cancelled);
        }

        if (!SaleAccess.MayCancel(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale!.Version != request.ExpectedVersion || sale.IsClosed)
        {
            return new(SaleStatus.Conflict);
        }

        var kind = sale.Status == SalesOrderStatus.Open
            ? ReservationReleaseKind.StaffCancelled
            : ReservationReleaseKind.DealCancelled;
        var releaseId = await transaction.ReleaseReservationAsync(kind, actorId, request.Reason!, cancellationToken);
        if (releaseId is null)
        {
            return new(SaleStatus.Conflict);
        }

        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Cancel(request.OperationId, actorId, request.Reason!, releaseId.Value, transaction.Context.Now),
            cancellationToken);
    }

    private async Task<SaleCommandResult> DecideAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        bool approve, CancellationToken cancellationToken)
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

        var kind = approve ? SalesOrderEventKind.Approved : SalesOrderEventKind.Rejected;
        if (transaction.Context.Existing is { } existing)
        {
            if (existing.Reason != request.Reason)
            {
                return new(SaleStatus.Conflict);
            }

            return SaleCommandSteps.Replayed(existing, saleId, actorId, request.ExpectedVersion, kind);
        }

        if (!SaleAccess.MayDecide(actor!, sale!))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale!.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        var now = transaction.Context.Now;
        if (approve)
        {
            return await SaleCommandSteps.ApplyAsync(transaction,
                () => sale.Approve(request.OperationId, actorId, now), cancellationToken);
        }

        return await SaleCommandSteps.ApplyAsync(transaction,
            () => sale.Reject(request.OperationId, actorId, request.Reason!, now), cancellationToken);
    }

    private static bool ValidAmount(decimal amount) => amount is >= 0m and <= MaximumTermAmount &&
        decimal.Round(amount, 2) == amount;
}
