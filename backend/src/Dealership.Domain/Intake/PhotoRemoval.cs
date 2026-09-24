using Dealership.Domain.Common;

namespace Dealership.Domain.Intake;

public enum PhotoRemovalReason
{
    UnsubmittedExpired,
    CancelledRetentionExpired,

    // The owner's account was erased: unattached uploads and photographs of appointments that never happened.
    OwnerErased,

    // The business-evidence period ended for the checked-in appointment the photograph belongs to.
    RetentionExpired
}

public sealed class PhotoRemoval : PhotoRemovalReceipt
{
    public Guid PhotoId { get; init; }
    public PhotoRemovalReason Reason { get; init; }
}
