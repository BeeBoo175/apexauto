using System.Net.Mail;
using Dealership.Domain.Staff;

namespace Dealership.Application.Accounts;

public sealed class StaffProvisioningService(IStaffProvisioningStore store)
{
    public Task<ProvisioningResult> ProvisionStaffAsync(StaffProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        var name = request.Name?.Trim();
        var mobile = request.Mobile?.Trim();

        if (request.Id == Guid.Empty || string.IsNullOrEmpty(email) ||
            email.Length > AccountCredentialPolicy.EmailMaximumLength ||
            !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > AccountCredentialPolicy.PasswordMaximumLength)
        {
            throw new ArgumentException("Staff identity and credentials are invalid.", nameof(request));
        }

        if (string.IsNullOrEmpty(name) || name.Length > StaffProfile.NameMaximumLength ||
            string.IsNullOrEmpty(mobile) || mobile.Length > StaffProfile.MobileMaximumLength)
        {
            throw new ArgumentException("Staff profile fields are invalid.", nameof(request));
        }

        // Bootstrap staff without a chosen username get one derived from their email by the store.
        var username = request.Username?.Trim();

        if (username is not null && !UsernamePolicy.IsValid(username))
        {
            throw new ArgumentException("Staff username is invalid.", nameof(request));
        }

        var branchScoped = request.Role is AccountRoles.SalesAgent or AccountRoles.WorkshopTechnician or AccountRoles.BranchManager;
        var groupScoped = request.Role is AccountRoles.GeneralManager or AccountRoles.Administrator;

        if ((!branchScoped && !groupScoped) ||
            (branchScoped && (!request.BranchId.HasValue || request.BranchId.Value == Guid.Empty)) ||
            (groupScoped && request.BranchId is not null))
        {
            throw new ArgumentException("Staff role and branch assignment are invalid.", nameof(request));
        }

        var staff = new ValidatedStaffProvisioning(request.Id, email, request.Password,
            request.Role, request.BranchId, name, mobile, username);
        return store.ProvisionStaffAsync(staff, cancellationToken);
    }
}
