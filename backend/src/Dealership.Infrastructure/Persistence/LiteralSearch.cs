namespace Dealership.Infrastructure.Persistence;

internal static class LiteralSearch
{
    public static string Pattern(string search)
    {
        return "%" + search.Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";
    }
}
