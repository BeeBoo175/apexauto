namespace Dealership.Application.Accounts;

public sealed record StaffProvisioningRequest(Guid Id, string Email, string Password, string Role,
    Guid? BranchId);

public sealed record ValidatedStaffProvisioning(Guid Id, string Email, string Password, string Role,
    Guid? BranchId);

public sealed record ProvisioningResult(bool Created, bool MatchesRequestedState);

public interface IStaffProvisioningStore
{
    Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff,
        CancellationToken cancellationToken);
}
