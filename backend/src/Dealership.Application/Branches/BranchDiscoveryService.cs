namespace Dealership.Application.Branches;

public sealed class BranchDiscoveryService(IPublicBranchStore branches)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public async Task<PublicBranchPage?> GetBranchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > MaximumPageSize)
        {
            return null;
        }

        return await branches.GetActiveAsync(page, pageSize, cancellationToken);
    }
}
