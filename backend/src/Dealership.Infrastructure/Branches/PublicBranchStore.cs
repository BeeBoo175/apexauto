using Dealership.Application.Branches;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Branches;

public sealed class PublicBranchStore(DealershipDbContext database) : IPublicBranchStore
{
    public async Task<PublicBranchPage> GetActiveAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var active = database.Branches.AsNoTracking().Where(branch => branch.IsActive);
        var total = await active.CountAsync(cancellationToken);
        var items = await active.OrderBy(branch => branch.Name).ThenBy(branch => branch.Id)
            .Select(branch => new PublicBranch(branch.Id, branch.Name, branch.Address, branch.City, branch.State, branch.Phone,
                branch.ShortCode))
            .ReadPageAsync(page, pageSize, total, cancellationToken);
        return new PublicBranchPage(items, total, page, pageSize);
    }

    public Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken)
    {
        return database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
    }
}
