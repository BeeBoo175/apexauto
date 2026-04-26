using Dealership.Application.Common;

namespace Dealership.Application.Vehicles;

// Scoped list request shared by master-data tables; ordering and filtering apply before paging.
public sealed record AdministrationListQuery(int Page, int PageSize, string? Search, string Status, string? Sort,
    bool Descending)
{
    public const int SearchMaximumLength = 160;
    public const string AllStatuses = "All";

    // Column filters: text on the identifying column, an amount range, an inclusive Malaysia-date range and a grade.
    public string? Text { get; init; }

    public decimal? MinimumAmount { get; init; }

    public decimal? MaximumAmount { get; init; }

    public ListDateRange? Dates { get; init; }

    public string? Grade { get; init; }

    public static AdministrationListQuery? Create(int page, int pageSize, string? search, string? status, string? sort,
        string? direction, IReadOnlySet<string> statuses, IReadOnlySet<string> sorts)
    {
        var term = NormalizedText(search);
        var state = status ?? AllStatuses;
        if (!Pagination.IsValid(page, pageSize) || term is false || !statuses.Contains(state) ||
            sort is not null && !sorts.Contains(sort) || direction is not (null or "asc" or "desc") ||
            direction is not null && sort is null)
        {
            return null;
        }

        return new(page, pageSize, search?.Trim() is { Length: > 0 } trimmed ? trimmed : null, state, sort,
            direction == "desc");
    }

    // Applies the column filters, returning null when any of them is invalid.
    public AdministrationListQuery? WithColumns(string? text, decimal? minimumAmount, decimal? maximumAmount,
        string? expiresFrom, string? expiresTo, string? grade)
    {
        var range = ListDateRange.Parse(expiresFrom, expiresTo);
        if (NormalizedText(text) is false || !MoneyRange.IsValid(minimumAmount, maximumAmount) || range is null ||
            grade is not (null or "A" or "B" or "C"))
        {
            return null;
        }

        return this with
        {
            Text = text?.Trim() is { Length: > 0 } trimmed ? trimmed : null,
            MinimumAmount = minimumAmount,
            MaximumAmount = maximumAmount,
            Dates = range.From is null ? null : range,
            Grade = grade
        };
    }

    // False when the value is too long or contains control characters; true or null otherwise.
    private static bool? NormalizedText(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is null ? null : trimmed.Length <= SearchMaximumLength && !trimmed.Any(char.IsControl);
    }
}
