namespace Dealership.Domain.Intake;

public enum PhotoRemovalReason
{
    UnsubmittedExpired,
    CancelledRetentionExpired,

    // The owner's account was erased: unattached uploads and photographs of appointments that never happened.
    OwnerErased,

    // The seven-year business-evidence period ended for the checked-in appointment the photograph belongs to.
    RetentionExpired
}

public sealed class PhotoRemoval
{
    public Guid PhotoId { get; init; }
    public PhotoRemovalReason Reason { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset EligibleAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? ClaimId { get; set; }
    public DateTimeOffset? ClaimExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
