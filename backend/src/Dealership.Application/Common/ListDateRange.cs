using System.Globalization;
using Dealership.Domain.Common;

namespace Dealership.Application.Common;

public sealed record ListDateRange(DateTimeOffset? From, DateTimeOffset? Until)
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static bool IsValid(DateTimeOffset? from, DateTimeOffset? until)
    {
        return from is null && until is null ||
            from is not null && until > from && until - from <= TimeSpan.FromDays(366);
    }

    public static ListDateRange? Parse(string? from, string? to)
    {
        if (from is null && to is null)
        {
            return new(null, null);
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) ||
            last < first || last.DayNumber - first.DayNumber >= 366 || first == DateOnly.MinValue || last == DateOnly.MaxValue)
        {
            return null;
        }

        return new(TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), Malaysia),
            TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), Malaysia));
    }
}
