using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

// The accepted queue and table inputs; anything else is refused before a read starts.
public static class SaleQueries
{
    public static readonly IReadOnlySet<string> QueueViews =
        new HashSet<string> { SaleViews.ToClaim, SaleViews.Handling, SaleViews.Waiting, SaleViews.Settling, SaleViews.Completed };

    public static readonly IReadOnlySet<string> TableViews =
        new HashSet<string> { SaleViews.All, SaleViews.NeedsApproval, SaleViews.Open, SaleViews.BeingHandled, SaleViews.Approved,
            SaleViews.AwaitingHandover, SaleViews.Delivered, SaleViews.History, SaleViews.AwaitingDeposit };

    public static readonly IReadOnlySet<string> Sorts = new HashSet<string>
    {
        "reference", "registration", "vehicle", "customer", "agent", "branch", "status", "discount",
        "timeLeft", "balance", "updated"
    };

    public static readonly IReadOnlySet<string> Outcomes = new HashSet<string> { "Expired", "Cancelled" };

    public static bool ValidQueue(string? view, string? search, int page, int pageSize) =>
        Pagination.IsValid(page, pageSize) && (view is null || QueueViews.Contains(view)) &&
        (search is null || ListSearch.IsValid(search));

    public static SaleTableQuery? Table(SaleTableInput input)
    {
        var page = input.Page ?? 1;
        var pageSize = input.PageSize ?? Pagination.DefaultPageSize;
        input = input with { View = QueryToken.ToDomain(input.View), Outcome = QueryToken.ToDomain(input.Outcome) };
        var normalized = input with
        {
            Search = Clean(input.Search),
            Handler = Clean(input.Handler),
            Branch = Clean(input.Branch),
            Registration = Clean(input.Registration),
            Vehicle = Clean(input.Vehicle),
            Customer = Clean(input.Customer),
            Reference = Clean(input.Reference)
        };
        var updated = ListDateRange.Parse(input.UpdatedFrom, input.UpdatedTo);
        var texts = new[] { normalized.Search, normalized.Handler, normalized.Branch, normalized.Registration,
            normalized.Vehicle, normalized.Customer, normalized.Reference };
        if (!Pagination.IsValid(page, pageSize) || input.View is not null && !TableViews.Contains(input.View) ||
            input.Sort is not null && !Sorts.Contains(input.Sort) || input.Direction is not (null or "asc" or "desc") ||
            input.Direction is not null && input.Sort is null || input.Outcome is not null && !Outcomes.Contains(input.Outcome) ||
            !MoneyRange.IsValid(input.MinimumDiscount, input.MaximumDiscount) ||
            !MoneyRange.IsValid(input.MinimumBalance, input.MaximumBalance) ||
            !ValidHours(input.MinimumHoursLeft, input.MaximumHoursLeft) ||
            updated is null || texts.Any(item => item is not null && !ListSearch.IsValid(item)))
        {
            return null;
        }

        return new SaleTableQuery(page, pageSize, input.View, normalized.Search, input.Sort, input.Direction == "desc")
        {
            Agent = normalized.Handler,
            Branch = normalized.Branch,
            Outcome = input.Outcome,
            Registration = normalized.Registration,
            Vehicle = normalized.Vehicle,
            Customer = normalized.Customer,
            Reference = normalized.Reference,
            MinimumDiscount = input.MinimumDiscount,
            MaximumDiscount = input.MaximumDiscount,
            MinimumBalance = input.MinimumBalance,
            MaximumBalance = input.MaximumBalance,
            MinimumHoursLeft = input.MinimumHoursLeft,
            MaximumHoursLeft = input.MaximumHoursLeft,
            Updated = updated.From is null ? null : updated
        };
    }

    private static string? Clean(string? value) => value?.Trim() is { Length: > 0 } text ? text : null;

    private static bool ValidHours(decimal? minimum, decimal? maximum) =>
        minimum is not < 0 && maximum is not < 0 &&
        minimum is not > PurchaseSettings.MaximumReservationHoldHours &&
        maximum is not > PurchaseSettings.MaximumReservationHoldHours &&
        (minimum is null || maximum is null || minimum <= maximum);
}
