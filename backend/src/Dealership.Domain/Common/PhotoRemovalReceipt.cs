namespace Dealership.Domain.Common;

// Claim and retry state shared by the durable photo removal receipts.
public abstract class PhotoRemovalReceipt
{
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset EligibleAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? ClaimId { get; set; }
    public DateTimeOffset? ClaimExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Guid StartAttempt(DateTimeOffset now)
    {
        Attempts = Math.Min(Attempts, int.MaxValue - 1) + 1;
        ClaimId = Guid.NewGuid();
        ClaimExpiresAt = now.AddMinutes(1);
        NextAttemptAt = now.AddMinutes(Math.Min(30, Math.Pow(2, Math.Min(Attempts - 1, 5))));
        return ClaimId.Value;
    }
}