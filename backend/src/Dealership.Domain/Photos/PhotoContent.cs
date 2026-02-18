namespace Dealership.Domain.Photos;

public sealed record PhotoContent(string ContentType, long Bytes, string Sha256, int Width, int Height)
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    public bool IsValid => IsSupportedContentType(ContentType) && Bytes is >= 1 and <= MaximumBytes &&
        Width > 0 && Height > 0 && Sha256.Length == 64 &&
        Sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsSupportedContentType(string? value)
    {
        return value is "image/jpeg" or "image/png" or "image/webp";
    }
}
