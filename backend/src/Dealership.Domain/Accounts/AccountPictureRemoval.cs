using Dealership.Domain.Common;

namespace Dealership.Domain.Accounts;

public enum AccountPictureRemovalReason
{
    Removed,
    Replaced,
    UnconfirmedExpired
}

// Durable intent to delete one picture object; the receipt keeps the claim and deletion history.
public sealed class AccountPictureRemoval : PhotoRemovalReceipt
{
    public Guid PictureId { get; init; }
    public AccountPictureRemovalReason Reason { get; init; }
}
