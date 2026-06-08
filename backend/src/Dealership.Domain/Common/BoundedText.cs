namespace Dealership.Domain.Common;

public static class BoundedText
{
    public const int MaximumLength = 1000;

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
