using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed record PublicBranch(Guid Id, string Name, string Address, string City, string State, string Phone);

public sealed record PublicBranchPage(IReadOnlyList<PublicBranch> Items, int Total, int Page, int PageSize);

public interface IPublicBranchStore
{
    Task<PublicBranchPage> GetActiveAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken);
}

public enum SlotDiscoveryStatus
{
    Succeeded,
    InvalidDate,
    BranchUnavailable
}

public sealed record InspectionSlotOffer(Guid BranchId, DateOnly Date, InspectionDateWindow DateWindow,
    IReadOnlyList<InspectionSlot> Slots);

public sealed record SlotDiscoveryResult(SlotDiscoveryStatus Status, InspectionSlotOffer? Offer = null);
