using Dealership.Domain.Common;

namespace Dealership.Domain.Intake;

public static class AppointmentDay
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static DateTimeOffset StartUtc(DateTimeOffset instant)
    {
        var date = TimeZoneInfo.ConvertTime(instant, Malaysia).Date;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date, Malaysia));
    }

    public static DateTimeOffset EndsAt(DateTimeOffset startsAt)
    {
        return StartUtc(startsAt).AddDays(1);
    }
}
