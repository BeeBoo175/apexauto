namespace Dealership.Domain.Intake;

public enum PhotoRemovalReason
{
    UnsubmittedExpired,
    CancelledRetentionExpired
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
