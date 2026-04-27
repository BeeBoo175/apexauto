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

    public static readonly IReadOnlySet<string> Views = new HashSet<string> { "All", "Active", "NeedsApproval", "History" };

    public static readonly IReadOnlySet<string> Sorts = new HashSet<string>
    {
        "number", "vehicle", "customer", "agent", "status", "balance", "discount", "submitted", "updated"
    };

    public static readonly IReadOnlySet<string> NextSteps = new HashSet<string>
    {
        "Add terms and submit", "Edit and resubmit", "Awaiting manager approval", "Approved", "Cancelled"
    };

    public Task<DealResult> ListAsync(Guid actorId, int page, int pageSize, string? view, string? status, string? sort,
        string? direction, string? number, string? vehicle, string? customer, string? search,
        DealColumnFilters columns, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        var texts = new[] { number, vehicle, customer, search, columns.Agent, columns.Branch }
            .Select(item => item?.Trim()).ToArray();
        var state = view ?? "All";
        var submitted = ListDateRange.Parse(columns.SubmittedFrom, columns.SubmittedTo);
        var updated = ListDateRange.Parse(columns.UpdatedFrom, columns.UpdatedTo);
        if (!Pagination.IsValid(page, pageSize) || !Views.Contains(state) ||
            status is not null && !Statuses.Contains(status) || sort is not null && !Sorts.Contains(sort) ||
            direction is not (null or "asc" or "desc") || direction is not null && sort is null ||
            columns.NextStep is not null && !NextSteps.Contains(columns.NextStep) ||
            !MoneyRange.IsValid(columns.MinimumBalance, columns.MaximumBalance) ||
            !MoneyRange.IsValid(columns.MinimumDiscount, columns.MaximumDiscount) || submitted is null ||
            updated is null || texts.Any(item => item is { Length: > 160 } || item?.Any(char.IsControl) == true))
        {
            return Rejected(DealStatus.Invalid);
        }

        static string? Clean(string? value) => string.IsNullOrEmpty(value) ? null : value;
        var query = new DealListQuery(page, pageSize, state, status, sort, direction == "desc", Clean(texts[0]),
            Clean(texts[1]), Clean(texts[2]), Clean(texts[3]))
        {
            Agent = Clean(texts[4]),
            Branch = Clean(texts[5]),
            NextStep = columns.NextStep,
            MinimumBalance = columns.MinimumBalance,
            MaximumBalance = columns.MaximumBalance,
            MinimumDiscount = columns.MinimumDiscount,
            MaximumDiscount = columns.MaximumDiscount,
            Submitted = submitted.From is null ? null : submitted,
            Updated = updated.From is null ? null : updated
        };
        return store.ListAsync(actorId, query, cancellationToken);
    }

    public Task<DealResult> VehiclesAsync(Guid actorId, string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        if (actorId == Guid.Empty)
        {
            return Rejected(DealStatus.Forbidden);
        }

        return term is { Length: > 160 } || term?.Any(char.IsControl) == true ? Rejected(DealStatus.Invalid)
            : store.VehiclesAsync(actorId, string.IsNullOrEmpty(term) ? null : term, clock.GetUtcNow(), cancellationToken);
    }

    public Task<DealResult> OptionsAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty ? Rejected(DealStatus.Forbidden)
            : dealId == Guid.Empty ? Rejected(DealStatus.Invalid)
            : store.OptionsAsync(actorId, dealId, cancellationToken);
    private static bool ValidAmount(decimal amount) => amount is >= 0m and <= PurchaseSettings.MaximumFee * 100 &&
        decimal.Round(amount, 2) == amount;

    private static Task<DealResult> Rejected(DealStatus status) => Task.FromResult(new DealResult(status));
}
