using Dealership.Application.Common;

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
    public static StaffWorklistQuery? Parse(string? from, string? to, string? status, Guid? branchId,
        int page, int pageSize, string? search = null, string? sort = null, string? direction = null)
    {
        var dates = ListDateRange.Parse(from, to);
        if (!Pagination.IsValid(page, pageSize) || branchId == Guid.Empty || dates is null)
        {
            return null;
        }

        var term = search?.Trim() ?? string.Empty;
        var order = sort ?? "startsAt";
        var orderDirection = direction ?? "asc";
        if (!ListSearch.IsValid(term) ||
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

        return new StaffWorklistQuery(dates.From, dates.Until, state.Value, branchId, page, pageSize, term, order, orderDirection);
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
