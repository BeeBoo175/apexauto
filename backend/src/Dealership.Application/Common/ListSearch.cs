namespace Dealership.Application.Common;

public static class ListSearch
{
    public static bool IsValid(string? search)
    {
        return search is not null && search.Length <= 160 && !search.Any(char.IsControl);
    }
}
