using Dealership.Application.Common;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

public sealed class DealService(IDealStore store, TimeProvider clock)
{
    public static readonly IReadOnlySet<string> Statuses = new HashSet<string>(Enum.GetNames<SalesOrderStatus>());

    public Task<DealResult> CreateAsync(Guid actorId, DealCreateRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        var email = request.CustomerEmail?.Trim();
        var byEmail = !string.IsNullOrEmpty(email);
        var byReservation = request.ReservationId is not null;
        // The customer comes from exactly one source: the reservation or an exact account email.
        if (request.Id == Guid.Empty || request.VehicleId == Guid.Empty || request.ReservationId == Guid.Empty ||
            byEmail == byReservation || byEmail && (email!.Length > 256 || email.Any(char.IsControl)))
        {
            return Rejected(DealStatus.Invalid);
        }

        return store.CreateAsync(actorId, request with { CustomerEmail = byEmail ? email : null }, clock.GetUtcNow(),
            cancellationToken);
    }

    public Task<DealResult> ReviseAsync(Guid actorId, Guid dealId, DealReviseRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        // A trade-in appointment and a positive allowance come together or not at all.
        var tradeInPaired = request.TradeInAppointmentId is null
            ? request.TradeInAllowance == 0m
            : request.TradeInAllowance > 0m;
        if (dealId == Guid.Empty || request.ExpectedVersion <= 0 || request.WarrantyPackageId == Guid.Empty ||
            request.TradeInAppointmentId == Guid.Empty || !ValidAmount(request.NegotiatedDiscount) ||
            !ValidAmount(request.TradeInAllowance) || !tradeInPaired)
        {
            return Rejected(DealStatus.Invalid);
        }

        return store.ReviseAsync(actorId, dealId, request, clock.GetUtcNow(), cancellationToken);
    }

    public Task<DealResult> TransitionAsync(Guid actorId, Guid dealId, DealAction action,
        DealTransitionRequest request, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        var needsReason = action is DealAction.Reject or DealAction.Cancel;
        if (dealId == Guid.Empty || request.ExpectedVersion <= 0 ||
            needsReason && !SalesOrder.ValidReason(request.Reason) || !needsReason && request.Reason is not null)
        {
            return Rejected(DealStatus.Invalid);
        }

        return store.TransitionAsync(actorId, dealId, action, request, clock.GetUtcNow(), cancellationToken);
    }

    public Task<DealResult> GetAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty ? Rejected(DealStatus.Forbidden)
            : dealId == Guid.Empty ? Rejected(DealStatus.Invalid)
            : store.GetAsync(actorId, dealId, cancellationToken);

    public Task<DealResult> ListAsync(Guid actorId, int page, int pageSize, string? status, string? number,
        CancellationToken cancellationToken)
    {
        var term = number?.Trim();
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        if (!Pagination.IsValid(page, pageSize) || status is not null && !Statuses.Contains(status) ||
            term is { Length: > 30 } || term?.Any(char.IsControl) == true)
        {
            return Rejected(DealStatus.Invalid);
        }

        return store.ListAsync(actorId, page, pageSize, status, string.IsNullOrEmpty(term) ? null : term,
            cancellationToken);
    }

    private static bool ValidAmount(decimal amount) => amount is >= 0m and <= PurchaseSettings.MaximumFee * 100 &&
        decimal.Round(amount, 2) == amount;

    private static Task<DealResult> Rejected(DealStatus status) => Task.FromResult(new DealResult(status));
}
