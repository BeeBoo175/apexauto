namespace Dealership.Domain.Common;

public sealed record AppointmentSlot(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record AppointmentDateWindow(DateOnly FirstDate, DateOnly LastDate);

public sealed class AppointmentSchedule
{
    private readonly TimeOnly _opensAt;
    private readonly TimeOnly _closesAt;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _notice;
    private readonly int _horizonDays;
    private readonly HashSet<DayOfWeek> _openDays;

    public AppointmentSchedule(TimeOnly opensAt, TimeOnly closesAt, int slotMinutes, int horizonDays,
        int noticeMinutes, IReadOnlyCollection<DayOfWeek> openDays)
    {
        ArgumentNullException.ThrowIfNull(openDays);

        if (closesAt <= opensAt || slotMinutes <= 0 ||
            slotMinutes > (closesAt - opensAt).TotalMinutes || horizonDays is < 1 or > 366 ||
            noticeMinutes < 0 || openDays.Count == 0 || openDays.Any(day => !Enum.IsDefined(day)))
        {
            throw new ArgumentException("Inspection schedule requires a valid same-day window, duration, horizon, notice and weekdays.");
        }

        _opensAt = opensAt;
        _closesAt = closesAt;
        _duration = TimeSpan.FromMinutes(slotMinutes);
        _horizonDays = horizonDays;
        _notice = TimeSpan.FromMinutes(noticeMinutes);
        _openDays = [.. openDays];
    }

    public AppointmentDateWindow GetDateWindow(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, DealershipLocale.Malaysia).DateTime);
        return new AppointmentDateWindow(today, today.AddDays(_horizonDays - 1));
    }

    public AppointmentSlot? FindSlot(DateTimeOffset startsAt, DateTimeOffset now)
    {
        return FindSlot(startsAt, now, _duration);
    }

    public AppointmentSlot? FindSlot(DateTimeOffset startsAt, DateTimeOffset now, TimeSpan duration)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, DealershipLocale.Malaysia).DateTime);
        return GetSlots(date, now, duration).SingleOrDefault(slot => slot.StartsAt == startsAt);
    }

    public IReadOnlyList<AppointmentSlot> GetSlots(DateOnly date, DateTimeOffset now)
    {
        return GetSlots(date, now, _duration);
    }

    public IReadOnlyList<AppointmentSlot> GetSlots(DateOnly date, DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > _closesAt - _opensAt ||
            duration.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Slots require whole minutes within the same-day opening window.");
        }

        var window = GetDateWindow(now);

        if (date < window.FirstDate || date > window.LastDate || !_openDays.Contains(date.DayOfWeek))
        {
            return [];
        }

        var localStart = date.ToDateTime(_opensAt, DateTimeKind.Unspecified);
        var localClose = date.ToDateTime(_closesAt, DateTimeKind.Unspecified);
        var earliest = now + _notice;
        var slots = new List<AppointmentSlot>();

        for (var start = localStart; start + duration <= localClose; start += duration)
        {
            var startsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, DealershipLocale.Malaysia));

            if (startsAt >= earliest)
            {
                slots.Add(new AppointmentSlot(startsAt, startsAt + duration));
            }
        }

        return slots;
    }
}
