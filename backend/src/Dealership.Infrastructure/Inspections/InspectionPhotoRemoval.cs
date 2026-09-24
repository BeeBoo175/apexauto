using Dealership.Domain.Common;

namespace Dealership.Infrastructure.Inspections;

public enum InspectionPhotoRemovalReason
{
    // An upload that expired without ever being attached to a saved checklist.
    UnattachedExpired,

    // The business-evidence period ended for the signed inspection the photograph belongs to.
    RetentionExpired
}

public sealed class InspectionPhotoRemoval : PhotoRemovalReceipt
{
    public Guid PhotoId { get; init; }
    public InspectionPhotoRemovalReason Reason { get; init; }
}
