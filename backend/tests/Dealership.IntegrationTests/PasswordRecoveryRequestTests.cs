using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class PasswordRecoveryRequestTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedRequestRollsBackInvalidationAndDiscardsIntentBeforeSameScopeRetry(bool cancelled)
    {
        var interruption = new RequestInterruption();
        var clock = new RecoveryClock();
        using var host = new PasswordRecoveryHost(database, clock, interruption);
        var account = await host.RegisterAsync();
        var signal = host.Factory.Services.GetRequiredService<MailDeliverySignal>();
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await host.RequestAsync(account.Email!);
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var original = await host.LatestAsync(account.Email!);
        clock.Now += PasswordRecoveryPolicy.RequestCooldown;
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>();
        var request = new PasswordRecoveryRequest(account.Email);
        using var cancellation = new CancellationTokenSource();
        interruption.Fail = true;
        interruption.Cancellation = cancelled ? cancellation : null;

        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RequestAsync(request, cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(request, cancellation.Token));
        }

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        var retained = await context.PasswordResetChallenges.AsNoTracking()
            .SingleAsync(challenge => challenge.UserId == original.UserId);
        Assert.Equal(original.Id, retained.Id);
        Assert.Null(retained.InvalidatedAt);
        Assert.Null(retained.ConsumedAt);
        using var waitingCancellation = new CancellationTokenSource();
        var waiting = signal.WaitAsync(waitingCancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await waitingCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        interruption.Fail = false;
        await service.RequestAsync(request, CancellationToken.None);
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        var challenges = await context.PasswordResetChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == original.UserId).ToArrayAsync();
        Assert.Equal(2, challenges.Length);
        Assert.Equal(clock.Now, challenges.Single(challenge => challenge.Id == original.Id).InvalidatedAt);
        var current = challenges.Single(challenge => challenge.Id != original.Id);
        Assert.Equal(account.Email, current.RecipientEmail);
        Assert.Equal(clock.Now, current.CreatedAt);
        Assert.Equal(clock.Now + PasswordRecoveryPolicy.Lifetime, current.ExpiresAt);
    }

    private sealed class RequestInterruption : SaveChangesInterceptor
    {
        public bool Fail { get; set; }

        public CancellationTokenSource? Cancellation { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fail || !eventData.Context!.ChangeTracker.Entries<PasswordResetChallenge>()
                .Any(entry => entry.State == EntityState.Added))
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            Cancellation?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Interrupted recovery intent write.");
        }
    }
}
