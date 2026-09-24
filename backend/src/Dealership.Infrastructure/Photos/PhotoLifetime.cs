namespace Dealership.Infrastructure.Photos;

internal static class PhotoLifetime
{
    // PostgreSQL stores microseconds, so a lifetime keeps whole microseconds to round-trip unchanged.
    public static TimeSpan Round(TimeSpan lifetime)
    {
        return TimeSpan.FromTicks(lifetime.Ticks - lifetime.Ticks % 10);
    }
}
