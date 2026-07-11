using System.Diagnostics.CodeAnalysis;
using Dealership.Domain.Common;

namespace Dealership.Domain.Accounts;

public enum AccountEventKind
{
    UsernameChanged,
    PictureRemoved,
    ProfileEdited,
    EmailChanged,
    BranchTransferred,
    Suspended,
    SuspensionLifted,
    Deactivated,
    Reactivated,
    NricRevealed,
    SessionsRevoked,
    VerificationResent,
    ResetSent
}

// One field an action changed, with the values either side as plain invariant text.
public sealed record AccountChange(string Field, string? Before, string? After);

// The account's only audit history: one append-only receipt per administrative or self-service change, keyed by
// the action's operation identity. Actor and target stay referenced by identity, so later edits never rewrite it.
public sealed class AccountEvent
{
    public const int ReasonMaximumLength = BoundedText.MaximumLength;

    private AccountEvent()
    {
    }

    public AccountEvent(Guid operationId, Guid targetId, Guid actorId, AccountEventKind kind, string? reason,
        IReadOnlyList<AccountChange>? changes, DateTimeOffset recordedAt)
    {
        reason = reason?.Trim();
        if (operationId == Guid.Empty || targetId == Guid.Empty || actorId == Guid.Empty ||
            !Enum.IsDefined(kind) || !IsValidReasonFor(kind, reason) || recordedAt == default)
        {
            throw new ArgumentException("An account event requires valid identities, a known kind and its reason.");
        }

        Id = operationId;
        TargetId = targetId;
        ActorId = actorId;
        Kind = kind;
        Reason = reason;
        Changes = changes is { Count: > 0 } ? changes : null;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid ActorId { get; private set; }

    public AccountEventKind Kind { get; private set; }

    public string? Reason { get; private set; }

    public IReadOnlyList<AccountChange>? Changes { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    // Every administrative action except lifting a suspension or reactivating carries the administrator's reason;
    // self-service changes and those two lifts do not.
    public static bool RequiresReason(AccountEventKind kind)
    {
        return kind is AccountEventKind.Suspended or AccountEventKind.Deactivated or AccountEventKind.BranchTransferred
            or AccountEventKind.NricRevealed or AccountEventKind.SessionsRevoked or AccountEventKind.VerificationResent
            or AccountEventKind.ResetSent or AccountEventKind.ProfileEdited or AccountEventKind.EmailChanged;
    }

    // The one rule for an administrator's reason; services trim before storing.
    public static bool IsValidReason([NotNullWhen(true)] string? reason)
    {
        return BoundedText.IsValidRequired(reason);
    }

    private static bool IsValidReasonFor(AccountEventKind kind, string? reason)
    {
        if (!RequiresReason(kind))
        {
            return reason is null;
        }

        return IsValidReason(reason);
    }
}
