namespace Dealership.Application.Accounts;

public sealed record BranchProvisioningRequest(Guid Id, string Name, string Address, string City,
    string State, string Phone);

public sealed record StaffProvisioningRequest(Guid Id, string Email, string Password, string Role,
    Guid? BranchId);

public sealed record ValidatedStaffProvisioning(Guid Id, string Email, string Password, string Role,
    Guid? BranchId);

public sealed record ProvisioningResult(bool Created, bool MatchesRequestedState);

public interface IStaffProvisioningStore
{
    Task<ProvisioningResult> ProvisionBranchAsync(Dealership.Domain.Branches.Branch branch,
        CancellationToken cancellationToken);

    Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff,
        CancellationToken cancellationToken);
}
