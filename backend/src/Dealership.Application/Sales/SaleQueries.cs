using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

// The accepted queue and table inputs; anything else is refused before a read starts.
public static class SaleQueries
{
    public static readonly IReadOnlySet<string> QueueViews =
        new HashSet<string> { "ToClaim", "Handling", "Waiting", "Approved" };

    public static readonly IReadOnlySet<string> TableViews =
        new HashSet<string> { "NeedsApproval", "Open", "BeingHandled", "Approved", "History" };

    public static readonly IReadOnlySet<string> Sorts = new HashSet<string>
    {
        "reference", "registration", "vehicle", "customer", "agent", "branch", "status", "discount",
        "timeLeft", "balance", "updated"
    };

    public static readonly IReadOnlySet<string> Outcomes = new HashSet<string> { "Expired", "Cancelled" };

    public static bool ValidQueue(string? view, string? search, int page, int pageSize) =>
        Pagination.IsValid(page, pageSize) && (view is null || QueueViews.Contains(view)) &&
        (search is null || ListSearch.IsValid(search));

    public static SaleTableQuery? Table(int page, int pageSize, string? view, string? search, string? sort,
        string? direction, string? agent, string? branch, string? outcome, decimal? minimumDiscount,
        decimal? maximumDiscount, decimal? minimumBalance, decimal? maximumBalance, string? registration,
        string? vehicle, string? customer, string? reference, decimal? minimumHoursLeft,
        decimal? maximumHoursLeft, string? updatedFrom, string? updatedTo)
    {
        var texts = new[] { search, agent, branch, registration, vehicle, customer, reference }
            .Select(item => item?.Trim()).ToArray();
        var updated = ListDateRange.Parse(updatedFrom, updatedTo);
        if (!Pagination.IsValid(page, pageSize) || view is not null && !TableViews.Contains(view) ||
            sort is not null && !Sorts.Contains(sort) || direction is not (null or "asc" or "desc") ||
            direction is not null && sort is null || outcome is not null && !Outcomes.Contains(outcome) ||
            !MoneyRange.IsValid(minimumDiscount, maximumDiscount) || !MoneyRange.IsValid(minimumBalance, maximumBalance) ||
            !ValidHours(minimumHoursLeft, maximumHoursLeft) ||
            updated is null || texts.Any(item => item is not null && !ListSearch.IsValid(item)))
        {
            return null;
        }

        return new SaleTableQuery(page, pageSize, view, Clean(texts[0]), sort, direction == "desc")
        {
            Agent = Clean(texts[1]),
            Branch = Clean(texts[2]),
            Outcome = outcome,
            Registration = Clean(texts[3]),
            Vehicle = Clean(texts[4]),
            Customer = Clean(texts[5]),
            Reference = Clean(texts[6]),
            MinimumDiscount = minimumDiscount,
            MaximumDiscount = maximumDiscount,
            MinimumBalance = minimumBalance,
            MaximumBalance = maximumBalance,
            MinimumHoursLeft = minimumHoursLeft,
            MaximumHoursLeft = maximumHoursLeft,
            Updated = updated.From is null ? null : updated
        };
    }

    private static string? Clean(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static bool ValidHours(decimal? minimum, decimal? maximum) =>
        minimum is not < 0 && maximum is not < 0 &&
        minimum is not > PurchaseSettings.MaximumReservationHoldHours &&
        maximum is not > PurchaseSettings.MaximumReservationHoldHours &&
        (minimum is null || maximum is null || minimum <= maximum);
}
