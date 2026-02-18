using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dealership.IntegrationTests;

public sealed class MailDeliveryFailureTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RolledBackClaimCannotLeakIntoAnotherPurposesSave()
    {
        var interruption = new ClaimFailureInterceptor();
        using var host = new PasswordRecoveryHost(database, interceptor: interruption);
        var account = await host.RegisterAsync();
        await host.RequestAsync(account.Email!);
        interruption.FailNextClaim = true;
        var transport = new RecordingMailTransport(fail: false);
        using var worker = new MailDeliveryWorker(host.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            transport, new MailDeliverySignal(), NullLogger<MailDeliveryWorker>.Instance);

        await worker.DeliverBatchAsync(CancellationToken.None);

        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var verification = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync();
        var reset = await context.PasswordResetChallenges.AsNoTracking().SingleAsync();
        Assert.False(interruption.FailNextClaim);
        Assert.NotNull(reset.DeliveredAt);
        Assert.NotNull(verification.DeliveredAt);
        Assert.Equal(1, verification.DeliveryAttempts);
        Assert.Equal(1, reset.DeliveryAttempts);
    }

    private sealed class ClaimFailureInterceptor : SaveChangesInterceptor
    {
        public bool FailNextClaim { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!FailNextClaim || eventData.Context?.ChangeTracker.Entries<EmailVerificationChallenge>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.DeliveryAttempts > 0) != true)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            FailNextClaim = false;
            throw new InvalidOperationException("Interrupted mail claim write.");
        }
    }
}
