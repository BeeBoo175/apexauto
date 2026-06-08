namespace Dealership.Domain.Common;

public static class UtcTime
{
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    }
}
