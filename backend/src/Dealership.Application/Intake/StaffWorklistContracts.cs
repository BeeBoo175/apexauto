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
    Guid? BranchId, int Page, int PageSize, string Search = "", string Sort = "startsAt", string Direction = "asc",
    string Registration = "", string Vehicle = "", string Customer = "", string Queue = "", Guid? HandlerId = null,
    string Handler = "")
{
    public static StaffWorklistQuery? Parse(string? from, string? to, string? status, Guid? branchId,
        int page, int pageSize, string? search = null, string? sort = null, string? direction = null,
        string? registration = null, string? vehicle = null, string? customer = null,
        string? queue = null, Guid? handlerId = null, string? handler = null)
    {
        var dates = ListDateRange.Parse(from, to);
        if (!Pagination.IsValid(page, pageSize) || branchId == Guid.Empty || handlerId == Guid.Empty || dates is null ||
            queue is not (null or "" or "ToClaim" or "MyWork" or "History"))
        {
            return null;
        }

        var term = search?.Trim() ?? string.Empty;
        var order = sort ?? "startsAt";
        var orderDirection = direction ?? "asc";
        if (!ListSearch.IsValid(term) ||
            order is not ("startsAt" or "registrationPlate" or "vehicle" or "customerName" or "branchName" or "status" or "handlerEmail") ||
            orderDirection is not ("asc" or "desc") ||
            !ListSearch.IsValid(registration ?? "") || !ListSearch.IsValid(vehicle ?? "") || !ListSearch.IsValid(customer ?? "") ||
            !ListSearch.IsValid(handler ?? ""))
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

        return new StaffWorklistQuery(dates.From, dates.Until, state.Value, branchId, page, pageSize, term, order, orderDirection,
            registration?.Trim() ?? "", vehicle?.Trim() ?? "", customer?.Trim() ?? "", queue ?? "", handlerId, handler?.Trim() ?? "");
    }
}

public sealed record StaffWorklistItem(Guid Id, Guid BranchId, string BranchName, string CustomerName,
    string RegistrationPlate, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status,
    string Make, string Model, string Trim, int ManufactureYear,
    Guid? HandlerId = null, string? HandlerEmail = null, bool HandlerEligible = false, long OwnershipVersion = 0);

// Section totals for the actor's branch scope, independent of dates, search and column filters.
public sealed record StaffWorklistCounts(int Booked, int CheckedIn, int Cancelled, int NoShow, int All,
    int ToClaim = 0, int MyWork = 0, int History = 0);

public sealed record StaffWorklistPage(IReadOnlyList<StaffWorklistItem> Items, int Total, int Page, int PageSize,
    StaffWorklistCounts? Counts = null);

public interface IStaffWorklistStore
{
    Task<StaffWorklistPage?> ListAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
    Task<StaffWorklistPage?> ExportAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
    Task<StaffWorklistPage?> CalendarAsync(Guid staffId, StaffWorklistQuery query, CancellationToken cancellationToken);
}
