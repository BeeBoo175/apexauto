using Dealership.Application.Accounts;
using Dealership.Application.Common;
using Dealership.Domain.Intake;

namespace Dealership.Application.TestDrives;

public sealed record TestDriveSlotSnapshot(DateTimeOffset Now, Guid BranchId, Guid PublicationId,
    IReadOnlyList<InspectionSlot> Occupied);

public sealed record TestDriveSlotOffer(Guid VehicleId, Guid BranchId, Guid PublicationId, DateOnly Date,
    InspectionDateWindow DateWindow, IReadOnlyList<InspectionSlot> Slots);

public enum TestDriveReadStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound
}

public sealed record TestDriveSlotResult(TestDriveReadStatus Status, TestDriveSlotOffer? Offer = null);

public sealed record TestDriveHistoryItem(Guid Id, Guid VehicleId, Guid BranchId, string BranchName,
    string Make, string Model, string Trim, int ManufactureYear, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, DateTimeOffset CreatedAt, string Status, DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt, DateTimeOffset? CancelledAt, string? CancellationReason, string? OutcomeReason);

public sealed record TestDriveHistory(CurrentActor Actor, IReadOnlyList<TestDriveHistoryItem> Items,
    int Total, int Page, int PageSize);

public sealed record TestDriveHistoryResult(TestDriveReadStatus Status, TestDriveHistory? History = null);

public interface ITestDriveReadStore
{
    Task<TestDriveSlotSnapshot?> SlotsAsync(Guid vehicleId, DateTimeOffset from, DateTimeOffset until,
        CancellationToken cancellationToken);

    Task<TestDriveHistoryResult> AccountAsync(Guid customerId, int page, ListDateRange dates,
        CancellationToken cancellationToken, string search = "");
}
