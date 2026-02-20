using Dealership.Application.Common;

namespace Dealership.Application.Branches;

public sealed class BranchDiscoveryService(IPublicBranchStore branches)
{
    public async Task<PublicBranchPage?> GetBranchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.IsValid(page, pageSize))
        {
            return null;
        }

        return await branches.GetActiveAsync(page, pageSize, cancellationToken);
    }
}
