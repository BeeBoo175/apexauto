using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dealership.IntegrationTests;

internal sealed class PasswordRecoveryInterruption : SaveChangesInterceptor
{
    public bool FailReceipt { get; set; }

    public bool PauseCredentials { get; set; }

    public TaskCompletionSource CredentialsReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context!;

        if (FailReceipt && context.ChangeTracker.Entries<PasswordResetChallenge>()
            .Any(entry => entry.State == EntityState.Modified && entry.Entity.ConsumedAt is not null))
        {
            throw new InvalidOperationException("Interrupted reset receipt write.");
        }

        if (PauseCredentials && context.ChangeTracker.Entries<AccountUser>()
            .Any(entry => entry.State == EntityState.Modified && entry.Property(user => user.PasswordHash).IsModified))
        {
            CredentialsReached.TrySetResult();
            await Resume.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }

        return result;
    }
}
