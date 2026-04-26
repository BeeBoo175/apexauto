using Dealership.Application.Common;

namespace Dealership.Application.Vehicles;

// Scoped list request shared by master-data tables; ordering and filtering apply before paging.
public sealed record AdministrationListQuery(int Page, int PageSize, string? Search, string Status, string? Sort,
    bool Descending)
{
    public const int SearchMaximumLength = 100;
    public const string AllStatuses = "All";

    public static AdministrationListQuery? Create(int page, int pageSize, string? search, string? status, string? sort,
        string? direction, IReadOnlySet<string> statuses, IReadOnlySet<string> sorts)
    {
        var term = search?.Trim();
        var state = status ?? AllStatuses;
        if (!Pagination.IsValid(page, pageSize) || term is { Length: > SearchMaximumLength } || term?.Any(char.IsControl) == true ||
            !statuses.Contains(state) || sort is not null && !sorts.Contains(sort) || direction is not (null or "asc" or "desc") ||
            direction is not null && sort is null)
        {
            return null;
        }

        return new(page, pageSize, string.IsNullOrEmpty(term) ? null : term, state, sort, direction == "desc");
    }
}
