namespace Dealership.Application.Common;

// A list's requested sort column and direction; an empty column keeps the list's canonical order.
public sealed record ListOrder(string Sort, bool Descending)
{
    public static bool TryParse(string? sort, string? direction, IReadOnlyCollection<string> allowedColumns,
        out ListOrder order)
    {
        order = new(string.Empty, false);
        if (!string.IsNullOrEmpty(sort) && !allowedColumns.Contains(sort))
        {
            return false;
        }

        if (direction is not (null or "" or "asc" or "desc"))
        {
            return false;
        }

        order = new(sort ?? string.Empty, direction == "desc");
        return true;
    }
}
