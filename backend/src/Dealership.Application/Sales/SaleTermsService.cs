using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using static Dealership.Application.Sales.SaleHandlingService;

namespace Dealership.Application.Sales;

// What a sale is agreed for and who decides it: the handling agent keys and submits terms, a manager approves or
// rejects an excess discount, and staff cancel with a reason, which refunds the deposit.
public sealed class SaleTermsService(ISaleCommandStore sales)
{
    public async Task<SaleCommandResult> SaveTermsAsync(Guid actorId, Guid saleId, SaleTermsRequest request,
        CancellationToken cancellationToken)
    {
        // A trade-in car and a positive allowance come together or not at all.
        var tradeInPaired = request.TradeInAppointmentId is null
            ? request.TradeInAllowance == 0m
            : request.TradeInAllowance > 0m;
        if (!Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) || !tradeInPaired ||
            request.WarrantyPackageId == Guid.Empty || request.TradeInAppointmentId == Guid.Empty ||
            !ValidAmount(request.NegotiatedDiscount) || !ValidAmount(request.TradeInAllowance))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
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
            return matches
                ? Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.TermsSaved)
                : new(SaleStatus.Conflict);
        }

        if (sale!.AgentId != actorId || !SaleAccess.IsAgent(actor!, sale.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        var settings = await transaction.LockSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return new(SaleStatus.Unavailable);
        }

        DealWarranty? warranty = null;
        if (request.WarrantyPackageId is { } packageId)
        {
            warranty = await transaction.ResolveWarrantyAsync(packageId, cancellationToken);
            if (warranty is null)
            {
                return new(SaleStatus.Invalid);
            }
        }

        DealTradeIn? tradeIn = null;
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

            tradeIn = new DealTradeIn(appointmentId, choice.Label, request.TradeInAllowance);
        }

        var terms = new DealTermsInput(warranty, request.NegotiatedDiscount, tradeIn);
        return await ApplyAsync(transaction, () => sale.SaveTerms(request.OperationId, terms, settings, actorId,
            transaction.Context.Now), cancellationToken);
    }

    public async Task<SaleCommandResult> SubmitAsync(Guid actorId, Guid saleId, SaleCommandRequest request,
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

        if (transaction.Context.Existing is { } existing)
        {
            return Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.Submitted);
        }

        if (sale!.AgentId != actorId || !SaleAccess.IsAgent(actor!, sale.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion || sale.Status != SalesOrderStatus.Handling)
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
        return await ApplyAsync(transaction, () => sale.Submit(request.OperationId, settings, number, actorId, now),
            cancellationToken);
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
        return SalesOrder.ValidReason(request.Reason)
            ? await DecideAsync(actorId, saleId, request, approve: false, cancellationToken)
            : new(SaleStatus.Invalid);
    }

    // Agents cancel only a sale they are still keying; managers cancel any sale that is not closed.
    public async Task<SaleCommandResult> CancelAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        CancellationToken cancellationToken)
    {
        if (!Valid(actorId, saleId, request.OperationId, request.ExpectedVersion) ||
            !SalesOrder.ValidReason(request.Reason))
        {
            return new(SaleStatus.Invalid);
        }

        await using var transaction = await sales.LockAsync(actorId, saleId, request.OperationId, cancellationToken);
        var (actor, sale, failure) = Scope(transaction.Context);
        if (failure is not null)
        {
            return new(failure.Value);
        }

        if (transaction.Context.Existing is { } existing)
        {
            return existing.Reason == request.Reason
                ? Replayed(existing, saleId, actorId, request.ExpectedVersion, SalesOrderEventKind.Cancelled)
                : new(SaleStatus.Conflict);
        }

        var manager = SaleAccess.IsManager(actor!, sale!.BranchId);
        var ownHandling = SaleAccess.IsAgent(actor!, sale.BranchId) && sale.AgentId == actorId &&
            sale.Status == SalesOrderStatus.Handling;
        if (!manager && !ownHandling)
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion || sale.IsClosed)
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

        return await ApplyAsync(transaction, () => sale.Cancel(request.OperationId, actorId, request.Reason!,
            releaseId.Value, transaction.Context.Now), cancellationToken);
    }

    private async Task<SaleCommandResult> DecideAsync(Guid actorId, Guid saleId, SaleReasonRequest request,
        bool approve, CancellationToken cancellationToken)
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

        var kind = approve ? SalesOrderEventKind.Approved : SalesOrderEventKind.Rejected;
        if (transaction.Context.Existing is { } existing)
        {
            return existing.Reason == request.Reason
                ? Replayed(existing, saleId, actorId, request.ExpectedVersion, kind)
                : new(SaleStatus.Conflict);
        }

        if (!SaleAccess.IsManager(actor!, sale!.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return new(SaleStatus.Conflict);
        }

        var now = transaction.Context.Now;
        return approve
            ? await ApplyAsync(transaction, () => sale.Approve(request.OperationId, actorId, now), cancellationToken)
            : await ApplyAsync(transaction, () => sale.Reject(request.OperationId, actorId, request.Reason!, now),
                cancellationToken);
    }

    private static bool ValidAmount(decimal amount) => amount is >= 0m and <= PurchaseSettings.MaximumFee * 100 &&
        decimal.Round(amount, 2) == amount;
}
