using System.Globalization;
using Dealership.Domain.Common;

namespace Dealership.Application.Intake;

public enum StaffWorklistStatus
{
    Booked,
    CheckedIn,
    Cancelled,
    All
}

public sealed record StaffWorklistQuery(DateTimeOffset From, DateTimeOffset Until, StaffWorklistStatus Status,
    Guid? BranchId, int Page, int PageSize)
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static StaffWorklistQuery? Parse(string? from, string? to, string? status, Guid? branchId,
        int page, int pageSize, DateTimeOffset now)
    {
        if (page < 1 || pageSize is < 1 or > 50 || branchId == Guid.Empty)
        {
            return null;
        }

        var state = status switch
        {
            null or "Booked" => StaffWorklistStatus.Booked,
            "CheckedIn" => StaffWorklistStatus.CheckedIn,
            "Cancelled" => StaffWorklistStatus.Cancelled,
            "All" => StaffWorklistStatus.All,
            _ => (StaffWorklistStatus?)null
        };
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Malaysia).DateTime);
        var last = first;

        if (state is null)
        {
            return null;
        }

        if ((from is not null || to is not null) &&
            (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out first) ||
                !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out last)))
        {
            return null;
        }

        if (last < first || last.DayNumber - first.DayNumber >= 366 || first == DateOnly.MinValue || last == DateOnly.MaxValue)
        {
            return null;
        }

        var lower = TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), Malaysia);
        var upper = TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), Malaysia);
        return new StaffWorklistQuery(lower, upper, state.Value, branchId, page, pageSize);
    }
}

public sealed record StaffWorklistItem(Guid Id, Guid BranchId, string BranchName, string CustomerName,
    string RegistrationPlate, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status);

public sealed record StaffWorklistPage(IReadOnlyList<StaffWorklistItem> Items, int Total, int Page, int PageSize);

public interface IStaffWorklistStore
{
    Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
}
