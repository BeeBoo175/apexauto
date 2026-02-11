using Dealership.Domain.Common;

namespace Dealership.Application.Intake;

public sealed class StaffCalendarService(IStaffWorklistStore worklist)
{
    public const int MaximumAppointments = 1000;
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static StaffWorklistQuery? Parse(string? from, string? to, string? status, Guid? branchId, DateTimeOffset now)
    {
        var query = StaffWorklistQuery.Parse(from, to, status ?? "All", branchId, 1, 20, now);
        return query is { From: not null, Until: not null } && query.Until - query.From <= TimeSpan.FromDays(42) ? query : null;
    }

    public async Task<StaffCalendarResult> ReadAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken)
    {
        if (query.From is not { } lower || query.Until is not { } upper)
        {
            throw new ArgumentException("Calendar reads require a bounded visible date range.", nameof(query));
        }
        var rows = await worklist.CalendarAsync(staffId, query, cancellationToken);
        if (rows is null)
        {
            return new StaffCalendarResult(StaffCalendarStatus.Forbidden);
        }
        if (rows.Total > MaximumAppointments)
        {
            return new StaffCalendarResult(StaffCalendarStatus.TooMany);
        }

        var from = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(lower, Malaysia).DateTime);
        var to = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(upper, Malaysia).DateTime).AddDays(-1);
        return new StaffCalendarResult(StaffCalendarStatus.Succeeded,
            new StaffCalendarSnapshot(rows.Items, rows.Total, from, to));
    }
}
