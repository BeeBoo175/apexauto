using Dealership.Domain.Branches;

namespace Dealership.Application.Branches;

public sealed class BranchProvisioningService(IBranchProvisioningStore store, TimeProvider clock)
{
    public Task<BranchProvisioningResult> ProvisionBranchAsync(BranchProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var branch = new Branch(request.Id, request.Name, request.Address, request.City,
            request.State, request.Phone, request.ShortCode, clock.GetUtcNow());
        return store.ProvisionBranchAsync(branch, cancellationToken);
    }
}
