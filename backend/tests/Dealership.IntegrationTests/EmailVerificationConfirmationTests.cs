using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class EmailVerificationConfirmationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Theory]
    [InlineData(false, AccountRoles.Customer, true)]
    [InlineData(true, AccountRoles.Administrator, true)]
    [InlineData(true, AccountRoles.Customer, false)]
    public async Task CurrentCustomerEligibilityPrecedesFirstConfirmationAndConsumedReplay(bool active, string role, bool hasProfile)
    {
        using var host = new PasswordRecoveryHost(database);

        foreach (var consumed in new[] { false, true })
        {
            var account = await host.RegisterAsync();
            using var scope = host.Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var challenge = await context.EmailVerificationChallenges.AsNoTracking()
                .SingleAsync(item => item.RecipientEmail == account.Email);
            var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
            var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();

            if (consumed)
            {
                Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
            }

            // Keep a stale tracked user to prove the locked command reads current eligibility.
            await context.Users.SingleAsync(user => user.Id == challenge.UserId);
            await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.IsActive, active).SetProperty(user => user.Role, role));

            if (!hasProfile)
            {
                var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(database.ConnectionString).Options;
                await using var maintenance = new DealershipDbContext(options);
                await maintenance.CustomerProfiles.Where(profile => profile.UserId == challenge.UserId).ExecuteDeleteAsync();
            }

            Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Empty(context.ChangeTracker.Entries());
            Assert.Equal(consumed, await context.Users.Where(user => user.Id == challenge.UserId)
                .Select(user => user.EmailConfirmed).SingleAsync());
            Assert.Equal(consumed, await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id)
                .Select(item => item.ConsumedAt != null).SingleAsync());
        }
    }

    [Fact]
    public async Task ExpiryBoundaryRejectsBothUnusedTokenAndConsumedReplay()
    {
        var clock = new RecoveryClock();
        using var host = new PasswordRecoveryHost(database, clock);
        var unused = await host.RegisterAsync();
        var consumed = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var first = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(item => item.RecipientEmail == unused.Email);
        var second = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(item => item.RecipientEmail == consumed.Email);
        var tokens = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>();
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        var token = tokens.Create(second);
        Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        var consumedAt = await context.EmailVerificationChallenges.Where(item => item.Id == second.Id)
            .Select(item => item.ConsumedAt).SingleAsync();
        clock.Now = second.ExpiresAt.AddTicks(-1);
        Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        Assert.Equal(consumedAt, await context.EmailVerificationChallenges.Where(item => item.Id == second.Id)
            .Select(item => item.ConsumedAt).SingleAsync());

        clock.Now = second.ExpiresAt;
        Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(tokens.Create(first), CancellationToken.None)).Status);
        Assert.False(await context.Users.Where(user => user.Id == first.UserId).Select(user => user.EmailConfirmed).SingleAsync());
    }

    [Fact]
    public async Task RecipientComparisonIsOrdinalAndReplayRequiresCurrentConfirmation()
    {
        using var host = new PasswordRecoveryHost(database);
        var account = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(item => item.RecipientEmail == account.Email);
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();
        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Email, account.Email!.ToUpperInvariant()));
        Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(token, CancellationToken.None)).Status);

        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Email, account.Email).SetProperty(user => user.EmailConfirmed, true));
        Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.EmailConfirmed, false));
        Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);

        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.EmailConfirmed, false));
        Assert.Equal(VerificationStatus.Invalid, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        Assert.NotNull(await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id)
            .Select(item => item.ConsumedAt).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentConfirmationSerializesAndConsumedRetryDoesNotUpdateIdentityAgain()
    {
        using var host = new PasswordRecoveryHost(database);
        var account = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(item => item.RecipientEmail == account.Email);
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
        var results = await Task.WhenAll(ConfirmAsync(), ConfirmAsync());
        Assert.All(results, result => Assert.Equal(VerificationStatus.Accepted, result.Status));
        var user = await context.Users.AsNoTracking().SingleAsync(item => item.Id == challenge.UserId);
        var consumedAt = await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id)
            .Select(item => item.ConsumedAt).SingleAsync();
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(consumedAt);

        Assert.Equal(VerificationStatus.Accepted, (await ConfirmAsync()).Status);
        var replayed = await context.Users.AsNoTracking().SingleAsync(item => item.Id == challenge.UserId);
        Assert.Equal(user.ConcurrencyStamp, replayed.ConcurrencyStamp);
        Assert.Equal(consumedAt, await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id)
            .Select(item => item.ConsumedAt).SingleAsync());

        async Task<VerificationResult> ConfirmAsync()
        {
            using var command = host.Factory.Services.CreateScope();
            return await command.ServiceProvider.GetRequiredService<EmailVerificationService>().ConfirmAsync(token, CancellationToken.None);
        }
    }

    [Fact]
    public async Task FailedConsumptionRollsBackIdentityAndAllowsExactRetryInSameScope()
    {
        var interruption = new ConfirmationInterruption();
        using var host = new PasswordRecoveryHost(database, interceptor: interruption);
        var account = await host.RegisterAsync();
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync(item => item.RecipientEmail == account.Email);
        var before = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
        var service = scope.ServiceProvider.GetRequiredService<EmailVerificationService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(token, CancellationToken.None));

        Assert.True(interruption.IdentityWriteObserved);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        var rolledBack = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        Assert.False(rolledBack.EmailConfirmed);
        Assert.Equal(before.ConcurrencyStamp, rolledBack.ConcurrencyStamp);
        Assert.Null(await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id).Select(item => item.ConsumedAt).SingleAsync());

        interruption.Fail = false;
        Assert.Equal(VerificationStatus.Accepted, (await service.ConfirmAsync(token, CancellationToken.None)).Status);
        Assert.Empty(context.ChangeTracker.Entries());
        var confirmed = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        Assert.True(confirmed.EmailConfirmed);
        Assert.Equal(before.SecurityStamp, confirmed.SecurityStamp);
        Assert.Equal(before.PasswordHash, confirmed.PasswordHash);
        Assert.NotNull(await context.EmailVerificationChallenges.Where(item => item.Id == challenge.Id).Select(item => item.ConsumedAt).SingleAsync());
    }

    private sealed class ConfirmationInterruption : SaveChangesInterceptor
    {
        public bool Fail { get; set; } = true;

        public bool IdentityWriteObserved { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;

            if (context.ChangeTracker.Entries<AccountUser>().Any(entry => entry.Entity.EmailConfirmed && entry.State == EntityState.Modified))
            {
                IdentityWriteObserved = true;
            }

            if (Fail && context.ChangeTracker.Entries<EmailVerificationChallenge>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.ConsumedAt is not null))
            {
                throw new InvalidOperationException("Interrupted verification consumption write.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
