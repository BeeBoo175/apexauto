namespace Dealership.Domain.Common;

public static class AppointmentDay
{

    public static DateTimeOffset StartUtc(DateTimeOffset instant)
    {
        var date = TimeZoneInfo.ConvertTime(instant, DealershipLocale.Malaysia).Date;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date, DealershipLocale.Malaysia));
    }

    public static DateTimeOffset EndsAt(DateTimeOffset startsAt)
    {
        return StartUtc(startsAt).AddDays(1);
    }
}
