using System.Net.Mail;

namespace Dealership.Application.Accounts;

public sealed class StaffProvisioningService(IStaffProvisioningStore store)
{
    public Task<ProvisioningResult> ProvisionStaffAsync(StaffProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (request.Id == Guid.Empty || string.IsNullOrEmpty(email) ||
            email.Length > AccountCredentialPolicy.EmailMaximumLength ||
            !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > AccountCredentialPolicy.PasswordMaximumLength)
        {
            throw new ArgumentException("Staff identity and credentials are invalid.", nameof(request));
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
            request.Role, request.BranchId);
        return store.ProvisionStaffAsync(staff, cancellationToken);
    }
}
