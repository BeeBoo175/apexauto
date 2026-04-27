using Dealership.Application.Accounts;
using Dealership.Application.Common;
using System.Text.Json.Serialization;

namespace Dealership.Application.Vehicles;

public sealed record ReservationReadFilter(Guid? BranchId = null, int Page = 1,
    int PageSize = Pagination.DefaultPageSize, bool? IsActive = null, string Sort = "", string Direction = "asc",
    string Search = "", string Registration = "", string Vehicle = "", string Buyer = "", string Status = "",
    decimal? MinimumTotal = null, decimal? MaximumTotal = null,
    DateTimeOffset? CreatedFrom = null, DateTimeOffset? CreatedUntil = null,
    DateTimeOffset? ExpiresFrom = null, DateTimeOffset? ExpiresUntil = null,
    DateTimeOffset? ReleasedFrom = null, DateTimeOffset? ReleasedUntil = null)
{
    public bool IsValid => BranchId != Guid.Empty && Pagination.IsValid(Page, PageSize) &&
        Sort is "" or "reference" or "registration" or "vehicle" or "buyer" or "branch" or "created" or "expires" or "released" or "total" or "status" &&
        Direction is "asc" or "desc" &&
        ListSearch.IsValid(Search) && ListSearch.IsValid(Registration) && ListSearch.IsValid(Vehicle) && ListSearch.IsValid(Buyer) &&
        Status is "" or "Active" or "CustomerCancelled" or "StaffCancelled" or "Expired" or "Withdrawn" or "Reinspection" &&
        MoneyRange.IsValid(MinimumTotal, MaximumTotal) &&
        ListDateRange.IsValid(CreatedFrom, CreatedUntil) && ListDateRange.IsValid(ExpiresFrom, ExpiresUntil) &&
        ListDateRange.IsValid(ReleasedFrom, ReleasedUntil);
}

public sealed record ReservationHistoryItem(ReservationReceipt Receipt, string BranchName,
    string Make, string Model, string Trim, int ManufactureYear, bool IsActive, ReservationRefundReceipt? Release,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RegistrationPlate,
    Guid? CoverPhotoId = null);

// Staff view tab totals for the authorized scope, independent of search and column filters.
public sealed record ReservationCounts(int Active, int History);

public sealed record ReservationHistoryPage(CurrentActor Actor, IReadOnlyList<ReservationHistoryItem> Items,
    int Total, int Page, int PageSize, ReservationHistoryItem? Active, ReservationCounts? Counts = null);

public sealed record ReservationHistoryResult(ReservationStatus Status, ReservationHistoryPage? History = null);

public sealed record ReservationDetailResult(ReservationStatus Status, CurrentActor? Actor = null,
    ReservationHistoryItem? Item = null);

public interface IReservationReadStore
{
    Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken);

    Task<ReservationHistoryResult> StaffAsync(Guid actorId, ReservationReadFilter filter, CancellationToken cancellationToken);

    Task<ReservationDetailResult> DetailAsync(Guid actorId, Guid reservationId, CancellationToken cancellationToken);
}
