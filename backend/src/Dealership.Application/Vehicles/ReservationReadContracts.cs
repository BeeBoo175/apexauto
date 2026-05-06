using Dealership.Application.Accounts;

namespace Dealership.Application.Vehicles;

// A customer's own reservation; staff work holds through their sale instead.
public sealed record ReservationHistoryItem(ReservationReceipt Receipt, string BranchName,
    string Make, string Model, string Trim, int ManufactureYear, bool IsActive, ReservationRefundReceipt? Release,
    Guid? CoverPhotoId = null)
{
    // An agent is handling the hold's sale: the customer is told to expect contact instead of seeing a countdown.
    public bool IsHandled { get; init; }

    // The sale's running deadline, which a released claim resumes; empty while an agent handles the sale.
    public DateTimeOffset? DueAt { get; init; }
}

public sealed record ReservationHistoryPage(CurrentActor Actor, IReadOnlyList<ReservationHistoryItem> Items,
    int Total, int Page, int PageSize, ReservationHistoryItem? Active);

public sealed record ReservationHistoryResult(ReservationStatus Status, ReservationHistoryPage? History = null);

public interface IReservationReadStore
{
    Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken);
}
