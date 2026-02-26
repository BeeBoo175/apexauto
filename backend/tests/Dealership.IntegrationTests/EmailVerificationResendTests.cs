using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class EmailVerificationResendTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Theory]
    [InlineData(false, AccountRoles.Customer, true)]
    [InlineData(true, AccountRoles.Administrator, true)]
    [InlineData(true, AccountRoles.Customer, false)]
    public async Task CurrentCustomerEligibilityPrecedesExactReplayAndConfirmedResponse(bool active, string role, bool hasProfile)
    {
        using var host = new PasswordRecoveryHost(database);
        var account = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var userId = await context.Users.Where(user => user.Email == account.Email).Select(user => user.Id).SingleAsync();
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        var key = Guid.NewGuid();
        Assert.Equal(VerificationStatus.Accepted, (await service.ResendAsync(userId, key, CancellationToken.None)).Status);

        await context.Users.Where(user => user.Id == userId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.IsActive, active)
            .SetProperty(user => user.Role, role)
            .SetProperty(user => user.EmailConfirmed, true));

        if (!hasProfile)
        {
            var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(database.ConnectionString).Options;
            await using var maintenance = new DealershipDbContext(options);
            await maintenance.CustomerProfiles.Where(profile => profile.UserId == userId).ExecuteDeleteAsync();
        }

        Assert.Equal(VerificationStatus.Forbidden, (await service.ResendAsync(userId, key, CancellationToken.None)).Status);
        Assert.Equal(VerificationStatus.Forbidden,
            (await service.ResendAsync(userId, Guid.NewGuid(), CancellationToken.None)).Status);
        Assert.Equal(2, await context.EmailVerificationChallenges.CountAsync(challenge => challenge.UserId == userId));
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AnotherAccountsReplayKeyIsDeniedBeforeAlreadyConfirmedResponse()
    {
        using var host = new PasswordRecoveryHost(database);
        var first = await host.RegisterAsync();
        var second = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        var firstId = await context.Users.Where(user => user.Email == first.Email).Select(user => user.Id).SingleAsync();
        var secondChallenge = await context.EmailVerificationChallenges.AsNoTracking()
            .SingleAsync(challenge => challenge.RecipientEmail == second.Email);
        var key = Guid.NewGuid();
        Assert.Equal(VerificationStatus.Accepted, (await service.ResendAsync(firstId, key, CancellationToken.None)).Status);
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(secondChallenge);
        Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);

        Assert.Equal(VerificationStatus.Forbidden,
            (await service.ResendAsync(secondChallenge.UserId, key, CancellationToken.None)).Status);
        Assert.Equal(VerificationStatus.Accepted,
            (await service.ResendAsync(secondChallenge.UserId, Guid.NewGuid(), CancellationToken.None)).Status);
        Assert.Equal(1, await context.EmailVerificationChallenges.CountAsync(challenge => challenge.UserId == secondChallenge.UserId));
    }

    [Fact]
    public async Task FailedResendRollsBackInvalidationAndDiscardsIntentBeforeExactRetry()
    {
        var interruption = new ResendInterruption();
        using var host = new PasswordRecoveryHost(database, interceptor: interruption);
        var account = await host.RegisterAsync();
        var signal = host.Factory.Services.GetRequiredService<MailDeliverySignal>();
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var initial = await context.EmailVerificationChallenges.AsNoTracking()
            .SingleAsync(challenge => challenge.RecipientEmail == account.Email);
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        var key = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResendAsync(initial.UserId, key, CancellationToken.None));

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        var retained = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(challenge => challenge.UserId == initial.UserId);
        Assert.Equal(initial.Id, retained.Id);
        Assert.Null(retained.InvalidatedAt);
        using var cancellation = new CancellationTokenSource();
        var waiting = signal.WaitAsync(cancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        interruption.Fail = false;
        Assert.Equal(VerificationStatus.Accepted, (await service.ResendAsync(initial.UserId, key, CancellationToken.None)).Status);
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var challenges = await context.EmailVerificationChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == initial.UserId).ToArrayAsync();
        Assert.Equal(2, challenges.Length);
        Assert.NotNull(challenges.Single(challenge => challenge.Id == initial.Id).InvalidatedAt);
        Assert.Equal(key, challenges.Single(challenge => challenge.Id != initial.Id).ResendKey);
    }

    private sealed class ResendInterruption : SaveChangesInterceptor
    {
        public bool Fail { get; set; } = true;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Fail && eventData.Context!.ChangeTracker.Entries<EmailVerificationChallenge>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.ResendKey is not null))
            {
                throw new InvalidOperationException("Interrupted resend intent write.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
