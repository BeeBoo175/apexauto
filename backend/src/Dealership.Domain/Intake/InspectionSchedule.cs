using Dealership.Domain.Common;

namespace Dealership.Domain.Intake;

public sealed record InspectionSlot(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record InspectionDateWindow(DateOnly FirstDate, DateOnly LastDate);

public sealed class InspectionSchedule
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
    private readonly TimeOnly _opensAt;
    private readonly TimeOnly _closesAt;
    private readonly TimeSpan _duration;
    private readonly TimeSpan _notice;
    private readonly int _horizonDays;
    private readonly HashSet<DayOfWeek> _openDays;

    public InspectionSchedule(TimeOnly opensAt, TimeOnly closesAt, int slotMinutes, int horizonDays,
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

    public InspectionDateWindow GetDateWindow(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Malaysia).DateTime);
        return new InspectionDateWindow(today, today.AddDays(_horizonDays - 1));
    }

    public InspectionSlot? FindSlot(DateTimeOffset startsAt, DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, Malaysia).DateTime);
        return GetSlots(date, now).SingleOrDefault(slot => slot.StartsAt == startsAt);
    }

    public IReadOnlyList<InspectionSlot> GetSlots(DateOnly date, DateTimeOffset now)
    {
        var window = GetDateWindow(now);

        if (date < window.FirstDate || date > window.LastDate || !_openDays.Contains(date.DayOfWeek))
        {
            return [];
        }

        var localStart = date.ToDateTime(_opensAt, DateTimeKind.Unspecified);
        var localClose = date.ToDateTime(_closesAt, DateTimeKind.Unspecified);
        var earliest = now + _notice;
        var slots = new List<InspectionSlot>();

        for (var start = localStart; start + _duration <= localClose; start += _duration)
        {
            var startsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, Malaysia));

            if (startsAt >= earliest)
            {
                slots.Add(new InspectionSlot(startsAt, startsAt + _duration));
            }
        }

        return slots;
    }
}
