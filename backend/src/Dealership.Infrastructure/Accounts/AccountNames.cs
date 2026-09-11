using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The recorded display name of an account: the staff profile name for staff and the legal name for customers.
// Reads that show who did something use it, so no screen has to derive a name from an email or a username.
internal static class AccountNames
{
    public static async Task<Dictionary<Guid, string>> ReadAsync(DealershipDbContext database,
        IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
        var names = await database.StaffProfiles.AsNoTracking().Where(profile => wanted.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.Name, cancellationToken);
        var customers = await database.CustomerProfiles.AsNoTracking()
            .Where(profile => wanted.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.LegalName ?? PersonLabels.FormerCustomer,
                cancellationToken);

        foreach (var (id, name) in customers)
        {
            names[id] = name;
        }

        return names;
    }
}
