using Dealership.Application.Common;

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
        "reference", "registration", "vehicle", "customer", "agent", "branch", "status", "discount", "overLimit",
        "submitted", "due", "claimed", "balance", "decided", "closed"
    };

    public static readonly IReadOnlySet<string> Outcomes = new HashSet<string> { "Expired", "Cancelled" };

    public static bool ValidQueue(string? view, string? search, int page, int pageSize) =>
        Pagination.IsValid(page, pageSize) && (view is null || QueueViews.Contains(view)) &&
        (search is null || ListSearch.IsValid(search));

    public static SaleTableQuery? Table(int page, int pageSize, string? view, string? search, string? sort,
        string? direction, string? agent, string? branch, string? outcome, decimal? minimumDiscount,
        decimal? maximumDiscount, decimal? minimumBalance, decimal? maximumBalance, string? submittedFrom,
        string? submittedTo, string? closedFrom, string? closedTo, string? registration, string? vehicle,
        string? customer, string? reference, decimal? minimumOverLimit, decimal? maximumOverLimit,
        string? decidedFrom, string? decidedTo)
    {
        var texts = new[] { search, agent, branch, registration, vehicle, customer, reference }
            .Select(item => item?.Trim()).ToArray();
        var submitted = ListDateRange.Parse(submittedFrom, submittedTo);
        var closed = ListDateRange.Parse(closedFrom, closedTo);
        var decided = ListDateRange.Parse(decidedFrom, decidedTo);
        if (!Pagination.IsValid(page, pageSize) || view is not null && !TableViews.Contains(view) ||
            sort is not null && !Sorts.Contains(sort) || direction is not (null or "asc" or "desc") ||
            direction is not null && sort is null || outcome is not null && !Outcomes.Contains(outcome) ||
            !MoneyRange.IsValid(minimumDiscount, maximumDiscount) || !MoneyRange.IsValid(minimumBalance, maximumBalance) ||
            !MoneyRange.IsValid(minimumOverLimit, maximumOverLimit) ||
            submitted is null || closed is null || decided is null || texts.Any(item => item is not null && !ListSearch.IsValid(item)))
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
            MinimumOverLimit = minimumOverLimit,
            MaximumOverLimit = maximumOverLimit,
            MinimumDiscount = minimumDiscount,
            MaximumDiscount = maximumDiscount,
            MinimumBalance = minimumBalance,
            MaximumBalance = maximumBalance,
            Submitted = submitted.From is null ? null : submitted,
            Closed = closed.From is null ? null : closed,
            Decided = decided.From is null ? null : decided
        };
    }

    private static string? Clean(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
