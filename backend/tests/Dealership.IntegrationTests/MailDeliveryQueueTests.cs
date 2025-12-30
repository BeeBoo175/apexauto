using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class MailDeliveryQueueTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task BothQueuesFenceCompetingClaimsRecoverLeasesAndExcludeStaleChallenges()
    {
        var clock = new RecoveryClock();
        using var host = new PasswordRecoveryHost(database, clock);

        foreach (var reset in new[] { false, true })
        {
            var (challengeId, _) = await CreateChallengeAsync(host, reset);
            var claims = await Task.WhenAll(ClaimAsync(host, reset), ClaimAsync(host, reset));
            var original = Assert.Single(claims, candidate => candidate is not null)!;
            Assert.Equal(challengeId, original.Id);
            Assert.Single(claims, candidate => candidate is null);

            clock.Now += TimeSpan.FromSeconds(31);
            Assert.Null(await ClaimAsync(host, reset));
            clock.Now += TimeSpan.FromSeconds(30);
            var reclaimed = Assert.IsType<ClaimedMail>(await ClaimAsync(host, reset));
            Assert.Equal(original.Id, reclaimed.Id);
            Assert.NotEqual(original.ClaimId, reclaimed.ClaimId);
            Assert.Equal(original.Message, reclaimed.Message);

            await AcknowledgeAsync(host, reset, original);
            Assert.Null(await DeliveredAtAsync(host, reset, original.Id));
            await AcknowledgeAsync(host, reset, reclaimed);
            Assert.NotNull(await DeliveredAtAsync(host, reset, original.Id));
            Assert.Null(await ClaimAsync(host, reset));

            await AssertStaleChallengesExcludedAsync(host, clock, reset);
        }
    }

    private static async Task AssertStaleChallengesExcludedAsync(PasswordRecoveryHost host, RecoveryClock clock, bool reset)
    {
        // Expiry is immutable; advance the clock rather than rewriting persisted challenge identity.
        await CreateChallengeAsync(host, reset);
        clock.Now += TimeSpan.FromHours(25);
        var (invalidatedId, _) = await CreateChallengeAsync(host, reset);
        var (_, consumedToken) = await CreateChallengeAsync(host, reset);

        using (var scope = host.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();

            if (reset)
            {
                await context.PasswordResetChallenges.Where(challenge => challenge.Id == invalidatedId)
                    .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, clock.Now));
                var result = await host.ResetAsync(consumedToken);
                Assert.Equal(PasswordResetStatus.Succeeded, result.Status);
            }
            else
            {
                await context.EmailVerificationChallenges.Where(challenge => challenge.Id == invalidatedId)
                    .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, clock.Now));
                var result = await scope.ServiceProvider.GetRequiredService<IEmailVerificationStore>()
                    .ConfirmAsync(consumedToken, CancellationToken.None);
                Assert.Equal(VerificationStatus.Accepted, result.Status);
            }
        }

        Assert.Null(await ClaimAsync(host, reset));
        var (currentId, _) = await CreateChallengeAsync(host, reset);
        var claimed = Assert.IsType<ClaimedMail>(await ClaimAsync(host, reset));
        Assert.Equal(currentId, claimed.Id);
        await AcknowledgeAsync(host, reset, claimed);
    }

    private static async Task<(Guid Id, string Token)> CreateChallengeAsync(PasswordRecoveryHost host, bool reset)
    {
        var account = await host.RegisterAsync();

        if (reset)
        {
            await host.RequestAsync(account.Email!);
            var challenge = await host.LatestAsync(account.Email!);
            return (challenge.Id, host.Token(challenge));
        }

        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var verification = await context.EmailVerificationChallenges.AsNoTracking()
            .SingleAsync(challenge => challenge.RecipientEmail == account.Email);
        return (verification.Id, scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(verification));
    }

    private static async Task<ClaimedMail?> ClaimAsync(PasswordRecoveryHost host, bool reset)
    {
        using var scope = host.Factory.Services.CreateScope();
        return await Queue(scope.ServiceProvider, reset).ClaimNextAsync(CancellationToken.None);
    }

    private static async Task AcknowledgeAsync(PasswordRecoveryHost host, bool reset, ClaimedMail mail)
    {
        using var scope = host.Factory.Services.CreateScope();
        await Queue(scope.ServiceProvider, reset).MarkDeliveredAsync(mail.Id, mail.ClaimId, CancellationToken.None);
    }

    private static IMailDeliverySource Queue(IServiceProvider services, bool reset)
    {
        var queues = services.GetServices<IMailDeliverySource>();
        return reset ? queues.OfType<PasswordResetMailSource>().Single() : queues.OfType<VerificationMailSource>().Single();
    }

    private static async Task<DateTimeOffset?> DeliveredAtAsync(PasswordRecoveryHost host, bool reset, Guid id)
    {
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();

        if (reset)
        {
            return await context.PasswordResetChallenges.Where(challenge => challenge.Id == id)
                .Select(challenge => challenge.DeliveredAt).SingleAsync();
        }

        return await context.EmailVerificationChallenges.Where(challenge => challenge.Id == id)
            .Select(challenge => challenge.DeliveredAt).SingleAsync();
    }
}
