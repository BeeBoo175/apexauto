using System.Globalization;
using Dealership.Domain.Common;

namespace Dealership.Application.Intake;

public sealed record AppointmentHistoryQuery(DateTimeOffset? From, DateTimeOffset? Until, int Page, int PageSize)
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static AppointmentHistoryQuery? Parse(string? from, string? to, int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 50)
        {
            return null;
        }

        if (from is null && to is null)
        {
            return new AppointmentHistoryQuery(null, null, page, pageSize);
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) ||
            last < first || last.DayNumber - first.DayNumber >= 366 || last == DateOnly.MaxValue)
        {
            return null;
        }

        var lower = TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), Malaysia);
        var upper = TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), Malaysia);
        return new AppointmentHistoryQuery(lower, upper, page, pageSize);
    }
}

public sealed record AppointmentHistoryItem(Guid Id, Guid BranchId, string BranchName, string RegistrationPlate,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt,
    DateTimeOffset? NoShowAt = null, DateTimeOffset? CheckedInAt = null);

public sealed record AppointmentHistoryPage(IReadOnlyList<AppointmentHistoryItem> Items, int Total, int Page, int PageSize);

public interface IAppointmentHistoryStore
{
    Task<AppointmentHistoryPage?> ListAsync(Guid ownerId, AppointmentHistoryQuery query, CancellationToken cancellationToken);
}
