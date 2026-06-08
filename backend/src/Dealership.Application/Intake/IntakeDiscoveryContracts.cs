using Dealership.Domain.Common;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public enum SlotDiscoveryStatus
{
    Succeeded,
    InvalidDate,
    BranchUnavailable
}

public sealed record InspectionSlotOffer(Guid BranchId, DateOnly Date, AppointmentDateWindow DateWindow,
    IReadOnlyList<AppointmentSlot> Slots);

public sealed record SlotDiscoveryResult(SlotDiscoveryStatus Status, InspectionSlotOffer? Offer = null);
