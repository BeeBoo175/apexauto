namespace Dealership.Application.Accounts;

public static class StaffInvitationPolicy
{
    public const int LifetimeHours = 72;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(LifetimeHours);
}

public enum StaffInvitationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    // The email already belongs to an account or another pending invitation.
    EmailTaken,
    UsernameTaken,
    RateLimited,
    // The invitation is revoked, activated or past its lifetime, or the link was replaced.
    Unavailable
}

public sealed record StaffInvitationRequest(Guid Id, string? Email, string? Role, Guid? BranchId, string? Name,
    string? Mobile);

public sealed record ValidatedStaffInvitation(Guid Id, string Email, string Role, Guid? BranchId, string Name,
    string Mobile);

public sealed record StaffInvitationActivation(string? Token, string? Password, string? Username);

// What the unindexed public page shows; opening it changes nothing.
public sealed record PublicStaffInvitation(string Name, string Role, string? BranchName);

public sealed record StaffInvitationResult(StaffInvitationStatus Status, TimeSpan? RetryAfter = null);

public sealed record PublicStaffInvitationResult(StaffInvitationStatus Status, PublicStaffInvitation? Invitation = null);

public interface IStaffInvitationStore
{
    Task<StaffInvitationResult> InviteAsync(Guid administratorId, ValidatedStaffInvitation invitation,
        CancellationToken cancellationToken);

    Task<StaffInvitationResult> ResendAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken);

    Task<StaffInvitationResult> ChangeEmailAsync(Guid administratorId, Guid invitationId, string email,
        CancellationToken cancellationToken);

    Task<StaffInvitationResult> RevokeAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken);

    Task<PublicStaffInvitationResult> ReadPublicAsync(string token, CancellationToken cancellationToken);

    Task<StaffInvitationResult> ActivateAsync(string token, string password, string username,
        CancellationToken cancellationToken);
}
