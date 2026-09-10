namespace Dealership.Domain.Accounts;

public enum AccountPictureRemovalReason
{
    Removed,
    Replaced,
    UnconfirmedExpired
}

// Durable intent to delete one picture object; the receipt keeps the claim and deletion history.
public sealed class AccountPictureRemoval
{
    public Guid PictureId { get; init; }
    public AccountPictureRemovalReason Reason { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset EligibleAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? ClaimId { get; set; }
    public DateTimeOffset? ClaimExpiresAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
