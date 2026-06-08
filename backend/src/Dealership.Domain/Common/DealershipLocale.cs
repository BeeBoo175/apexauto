namespace Dealership.Domain.Common;

public static class DealershipLocale
{
    public const string DefaultCurrency = "MYR";

    public const string MalaysiaTimeZoneId = "Asia/Kuala_Lumpur";

    public static TimeZoneInfo Malaysia { get; } = TimeZoneInfo.FindSystemTimeZoneById(MalaysiaTimeZoneId);
}
