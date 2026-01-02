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
        var offset = ((long)page - 1) * pageSize;

        if (offset >= total)
        {
            return new PublicBranchPage([], total, page, pageSize);
        }

        var items = await active.OrderBy(branch => branch.Name).ThenBy(branch => branch.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(branch => new PublicBranch(branch.Id, branch.Name, branch.Address, branch.City, branch.State, branch.Phone))
            .ToListAsync(cancellationToken);
        return new PublicBranchPage(items, total, page, pageSize);
    }

    public Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken)
    {
        return database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
    }
}
