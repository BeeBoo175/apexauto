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
            var matches = Matches(existing, new BranchProvisioningRequest(branch.Id, branch.Name,
                branch.Address, branch.City, branch.State, branch.Phone, branch.ShortCode));
            return new BranchProvisioningResult(false, matches);
        }

        database.Branches.Add(branch);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BranchProvisioningResult(true, true);
    }

    internal static bool Matches(Branch existing, BranchProvisioningRequest requested)
    {
        return existing.Name == requested.Name && existing.Address == requested.Address &&
            existing.City == requested.City && existing.State == requested.State &&
            existing.Phone == requested.Phone && existing.ShortCode == requested.ShortCode && existing.IsActive;
    }
}
