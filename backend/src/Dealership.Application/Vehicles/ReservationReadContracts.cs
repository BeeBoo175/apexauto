using Dealership.Application.Accounts;
using Dealership.Application.Common;

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

// A customer holds one car at a time, so a short page shows their whole recent history at a glance.
public static class ReservationHistory
{
    public const int PageSize = Pagination.AccountPageSize;
}

// A car the customer is paying the deposit for: held for them, but not reserved until the payment is confirmed.
public sealed record PendingPaymentItem(Guid RequestId, Guid VehicleId, string BranchName, string Make, string Model,
    string Trim, int ManufactureYear, DateTimeOffset ExpiresAt, string? PaymentUrl);

public sealed record ReservationHistoryPage(CurrentActor Actor, IReadOnlyList<ReservationHistoryItem> Items,
    int Total, int Page, int PageSize, ReservationHistoryItem? Active, PendingPaymentItem? PendingPayment = null);

public sealed record ReservationHistoryResult(ReservationStatus Status, ReservationHistoryPage? History = null);

public interface IReservationReadStore
{
    // Dates and search narrow the history only; the customer's active hold is always returned.
    Task<ReservationHistoryResult> AccountAsync(Guid customerId, int page, CancellationToken cancellationToken,
        ListDateRange? dates = null, string? search = null);
}
