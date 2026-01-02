using Dealership.Domain.Branches;

namespace Dealership.Application.Branches;

public sealed record BranchProvisioningRequest(Guid Id, string Name, string Address, string City,
    string State, string Phone);

public sealed record BranchProvisioningResult(bool Created, bool MatchesRequestedState);

public interface IBranchProvisioningStore
{
    Task<BranchProvisioningResult> ProvisionBranchAsync(Branch branch, CancellationToken cancellationToken);
}
