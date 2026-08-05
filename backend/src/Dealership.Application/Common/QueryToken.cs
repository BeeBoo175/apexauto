namespace Dealership.Application.Common;

public static class QueryToken
{
    // Queries carry camelCase tokens while domain names keep PascalCase. A token that does not start lower-case maps
    // to a value no accepted set contains, so the old PascalCase form is refused.
    public static string? ToDomain(string? token) => token switch
    {
        null => null,
        "" => "",
        [var first, ..] when char.IsLower(first) => char.ToUpperInvariant(first) + token[1..],
        _ => "invalid"
    };
}
