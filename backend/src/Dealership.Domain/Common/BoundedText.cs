using System.Diagnostics.CodeAnalysis;

namespace Dealership.Domain.Common;

public static class BoundedText
{
    public const int MaximumLength = 1000;

    // The trimmed text is present, within the maximum and free of control characters.
    public static bool IsValidRequired([NotNullWhen(true)] string? value)
    {
        var text = value?.Trim();
        return !string.IsNullOrEmpty(text) && text.Length <= MaximumLength && !text.Any(char.IsControl);
    }

    public static string? NormalizeOptional(string? value, string errorMessage)
    {
        if (value is null)
        {
            return null;
        }

        value = value.Trim();
        if (value.Length is < 1 or > MaximumLength || value.Any(char.IsControl))
        {
            throw new ArgumentException(errorMessage);
        }

        return value;
    }

    public static string NormalizeRequired(string value, string errorMessage)
    {
        // Required cancellation reasons reject controls before trimming, including leading newlines.
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new ArgumentException(errorMessage);
        }

        return NormalizeOptional(value, errorMessage)!;
    }
}
