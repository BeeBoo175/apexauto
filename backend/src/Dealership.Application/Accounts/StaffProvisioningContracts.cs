namespace Dealership.Application.Accounts;

public sealed record StaffProvisioningRequest(Guid Id, string Email, string Password, string Role,
    Guid? BranchId, string Name, string Mobile, string? Username = null);

public sealed record ValidatedStaffProvisioning(Guid Id, string Email, string Password, string Role,
    Guid? BranchId, string Name, string Mobile, string? Username);

public sealed record ProvisioningResult(bool Created, bool MatchesRequestedState);

public interface IStaffProvisioningStore
{
    Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff,
        CancellationToken cancellationToken);
}
