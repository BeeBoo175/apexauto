using Dealership.Application.Accounts;
using Dealership.Application.Common;

namespace Dealership.Application.Vehicles;

public sealed record ReservationReadFilter(Guid? BranchId = null, int Page = 1,
    int PageSize = Pagination.DefaultPageSize, bool? IsActive = null, string Sort = "", string Direction = "asc")
{
    public bool IsValid => BranchId != Guid.Empty && Pagination.IsValid(Page, PageSize) &&
        Sort is "" or "reference" or "vehicle" or "buyer" or "branch" or "created" or "expires" or "total" or "status" &&
        Direction is "asc" or "desc";
}

public sealed record ReservationHistoryItem(ReservationReceipt Receipt, string BranchName,
    string Make, string Model, string Trim, int ManufactureYear, bool IsActive, ReservationRefundReceipt? Release);

public sealed record ReservationHistoryPage(CurrentActor Actor, IReadOnlyList<ReservationHistoryItem> Items,
    int Total, int Page, int PageSize, ReservationHistoryItem? Active);

public sealed record ReservationHistoryResult(ReservationStatus Status, ReservationHistoryPage? History = null);

public interface IReservationReadStore
{
    Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken);

    Task<ReservationHistoryResult> StaffAsync(Guid actorId, ReservationReadFilter filter, CancellationToken cancellationToken);
}
