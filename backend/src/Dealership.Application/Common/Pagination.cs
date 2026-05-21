namespace Dealership.Application.Common;

public static class Pagination
{
    public const int DefaultPageSize = 25;

    // A customer has few records of each kind, so their own history lists show a short page.
    public const int AccountPageSize = 5;

    private static readonly int[] PageSizes = [10, 25, 50];

    // The accepted sizes as shown to callers, so messages cannot drift from the rule.
    public static string PageSizeChoices { get; } = $"{string.Join(", ", PageSizes[..^1])} or {PageSizes[^1]}";

    public static bool IsValid(int page, int pageSize)
    {
        return page >= 1 && PageSizes.Contains(pageSize);
    }

    public static bool IsValidAccount(int page, int pageSize)
    {
        return page >= 1 && pageSize == AccountPageSize;
    }
}
