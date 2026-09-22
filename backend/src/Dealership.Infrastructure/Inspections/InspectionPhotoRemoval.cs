namespace Dealership.Infrastructure.Inspections;

public enum InspectionPhotoRemovalReason
{
    // An upload that expired without ever being attached to a saved checklist.
    UnattachedExpired,

    // The business-evidence period ended for the signed inspection the photograph belongs to.
    RetentionExpired
}

public sealed class InspectionPhotoRemoval
{
    public Guid PhotoId { get; init; }
    public InspectionPhotoRemovalReason Reason { get; init; }
    public DateTimeOffset EligibleAt { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? ClaimId { get; set; }
    public DateTimeOffset? ClaimExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
