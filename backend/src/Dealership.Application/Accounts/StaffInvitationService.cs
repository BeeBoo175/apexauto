using System.Net.Mail;
using Dealership.Domain.Staff;

namespace Dealership.Application.Accounts;

// Administrators invite staff by email; the account exists only once the employee activates the link.
public sealed class StaffInvitationService(IStaffInvitationStore store)
{
    public Task<StaffInvitationResult> InviteAsync(Guid administratorId, StaffInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizedEmail(request.Email);
        var name = request.Name?.Trim();
        var mobile = request.Mobile?.Trim();
        var role = request.Role;
        var branchScoped = role is AccountRoles.SalesAgent or AccountRoles.WorkshopTechnician or AccountRoles.BranchManager;
        var groupScoped = role is AccountRoles.GeneralManager or AccountRoles.Administrator;

        if (request.Id == Guid.Empty || email is null || string.IsNullOrEmpty(name) ||
            name.Length > StaffProfile.NameMaximumLength || string.IsNullOrEmpty(mobile) ||
            mobile.Length > StaffProfile.MobileMaximumLength || (!branchScoped && !groupScoped) ||
            (branchScoped && (request.BranchId is null || request.BranchId == Guid.Empty)) ||
            (groupScoped && request.BranchId is not null))
        {
            return Invalid();
        }

        return store.InviteAsync(administratorId,
            new ValidatedStaffInvitation(request.Id, email, role!, request.BranchId, name, mobile), cancellationToken);
    }

    public Task<StaffInvitationResult> ResendAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken)
    {
        return invitationId == Guid.Empty ? Invalid() : store.ResendAsync(administratorId, invitationId, cancellationToken);
    }

    public Task<StaffInvitationResult> ChangeEmailAsync(Guid administratorId, Guid invitationId, string? email,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizedEmail(email);
        return invitationId == Guid.Empty || normalized is null ? Invalid()
            : store.ChangeEmailAsync(administratorId, invitationId, normalized, cancellationToken);
    }

    public Task<StaffInvitationResult> RevokeAsync(Guid administratorId, Guid invitationId,
        CancellationToken cancellationToken)
    {
        return invitationId == Guid.Empty ? Invalid() : store.RevokeAsync(administratorId, invitationId, cancellationToken);
    }

    public Task<PublicStaffInvitationResult> ReadPublicAsync(string? token, CancellationToken cancellationToken)
    {
        return IsToken(token) ? store.ReadPublicAsync(token!, cancellationToken)
            : Task.FromResult(new PublicStaffInvitationResult(StaffInvitationStatus.Unavailable));
    }

    public Task<StaffInvitationResult> ActivateAsync(StaffInvitationActivation request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();

        if (!IsToken(request.Token) || string.IsNullOrEmpty(request.Password) ||
            request.Password.Length > AccountCredentialPolicy.PasswordMaximumLength || !UsernamePolicy.IsValid(username))
        {
            return Invalid();
        }

        return store.ActivateAsync(request.Token!, request.Password, username!, cancellationToken);
    }

    private static bool IsToken(string? token) => !string.IsNullOrWhiteSpace(token) && token.Length <= 160;

    private static string? NormalizedEmail(string? value)
    {
        var email = value?.Trim();
        return !string.IsNullOrEmpty(email) && email.Length <= AccountCredentialPolicy.EmailMaximumLength &&
            MailAddress.TryCreate(email, out var parsed) && parsed.Address == email ? email : null;
    }

    private static Task<StaffInvitationResult> Invalid() =>
        Task.FromResult(new StaffInvitationResult(StaffInvitationStatus.Invalid));
}
