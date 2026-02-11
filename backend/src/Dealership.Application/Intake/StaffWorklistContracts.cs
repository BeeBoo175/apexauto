using System.Globalization;
using Dealership.Domain.Common;

namespace Dealership.Application.Intake;

public enum StaffWorklistStatus
{
    Booked,
    CheckedIn,
    Cancelled,
    NoShow,
    All
}

public sealed record StaffWorklistQuery(DateTimeOffset? From, DateTimeOffset? Until, StaffWorklistStatus Status,
    Guid? BranchId, int Page, int PageSize, string Search = "", string Sort = "startsAt", string Direction = "asc")
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static StaffWorklistQuery? Parse(string? from, string? to, string? status, Guid? branchId,
        int page, int pageSize, DateTimeOffset now, string? search = null, string? sort = null, string? direction = null)
    {
        if (page < 1 || pageSize is < 1 or > 50 || branchId == Guid.Empty)
        {
            return null;
        }

        var term = search?.Trim() ?? string.Empty;
        var order = sort ?? "startsAt";
        var orderDirection = direction ?? "asc";
        if (term.Length > 160 || term.Any(char.IsControl) ||
            order is not ("startsAt" or "registrationPlate" or "customerName" or "branchName" or "status") ||
            orderDirection is not ("asc" or "desc"))
        {
            return null;
        }

        var state = status switch
        {
            null or "Booked" => StaffWorklistStatus.Booked,
            "CheckedIn" => StaffWorklistStatus.CheckedIn,
            "Cancelled" => StaffWorklistStatus.Cancelled,
            "NoShow" => StaffWorklistStatus.NoShow,
            "All" => StaffWorklistStatus.All,
            _ => (StaffWorklistStatus?)null
        };
        if (state is null)
        {
            return null;
        }

        if (from is null && to is null)
        {
            return new StaffWorklistQuery(null, null, state.Value, branchId, page, pageSize, term, order, orderDirection);
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
        {
            return null;
        }

        if (last < first || last.DayNumber - first.DayNumber >= 366 || first == DateOnly.MinValue || last == DateOnly.MaxValue)
        {
            return null;
        }

        var lower = TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), Malaysia);
        var upper = TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), Malaysia);
        return new StaffWorklistQuery(lower, upper, state.Value, branchId, page, pageSize, term, order, orderDirection);
    }
}

public sealed record StaffWorklistItem(Guid Id, Guid BranchId, string BranchName, string CustomerName,
    string RegistrationPlate, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status);

public sealed record StaffWorklistPage(IReadOnlyList<StaffWorklistItem> Items, int Total, int Page, int PageSize);

public interface IStaffWorklistStore
{
    Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
    Task<StaffWorklistPage?> ExportAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
    Task<StaffWorklistPage?> CalendarAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
}
