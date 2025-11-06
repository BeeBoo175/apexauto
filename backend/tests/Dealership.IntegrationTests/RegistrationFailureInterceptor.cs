using Dealership.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dealership.IntegrationTests;

internal sealed class RegistrationFailureInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.ChangeTracker.Entries<CustomerProfile>()
            .Any(entry => entry.State == EntityState.Added) == true)
        {
            throw new InvalidOperationException("Interrupted profile persistence.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
