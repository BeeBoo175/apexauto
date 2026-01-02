using Dealership.Application.Branches;
using Dealership.Domain.Branches;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Branches;

public sealed class BranchProvisioningStore(DealershipDbContext database) : IBranchProvisioningStore
{
    public async Task<BranchProvisioningResult> ProvisionBranchAsync(Branch branch,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Transaction locks serialize fixture creation across processes, including an absent row.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({branch.Id.ToString()}, 0))", cancellationToken);
        var existing = await database.Branches.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == branch.Id, cancellationToken);

        if (existing is not null)
        {
            // A fixture rerun reports drift without overwriting a person's edits.
            var matches = existing.Name == branch.Name && existing.Address == branch.Address &&
                existing.City == branch.City && existing.State == branch.State &&
                existing.Phone == branch.Phone && existing.IsActive;
            return new BranchProvisioningResult(false, matches);
        }

        database.Branches.Add(branch);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BranchProvisioningResult(true, true);
    }
}
