using System.Globalization;
using System.Text;

namespace Dealership.Application.Accounts;

public static class UsernamePolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 30;

    public static bool IsValid(string? username)
    {
        return username is { Length: >= MinimumLength and <= MaximumLength } && username.All(IsAllowed);
    }

    // Accounts that predate usernames, and bootstrap staff, start from the email's local part.
    public static string DeriveFromEmail(string email)
    {
        var localPart = email.Split('@')[0];
        var builder = new StringBuilder();

        foreach (var character in localPart.Where(IsAllowed))
        {
            builder.Append(character);
        }

        var derived = builder.ToString().PadRight(MinimumLength, '0');
        return derived.Length > MaximumLength ? derived[..MaximumLength] : derived;
    }

    // A numeric suffix keeps the candidate within the maximum length while resolving a clash.
    public static string WithSuffix(string username, int suffix)
    {
        var text = suffix.ToString(CultureInfo.InvariantCulture);
        var stem = username.Length + text.Length > MaximumLength ? username[..(MaximumLength - text.Length)] : username;
        return stem + text;
    }

    private static bool IsAllowed(char character)
    {
        return character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_';
    }
}
