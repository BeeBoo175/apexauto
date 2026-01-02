using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public enum SlotDiscoveryStatus
{
    Succeeded,
    InvalidDate,
    BranchUnavailable
}

public sealed record InspectionSlotOffer(Guid BranchId, DateOnly Date, InspectionDateWindow DateWindow,
    IReadOnlyList<InspectionSlot> Slots);

public sealed record SlotDiscoveryResult(SlotDiscoveryStatus Status, InspectionSlotOffer? Offer = null);
