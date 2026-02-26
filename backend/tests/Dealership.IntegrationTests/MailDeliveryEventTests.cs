using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class MailDeliveryEventTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task RegistrationResendAndRecoveryWakeDeliveryAfterTheirIntentsCommit()
    {
        using var host = new PasswordRecoveryHost(database);
        var signal = host.Factory.Services.GetRequiredService<MailDeliverySignal>();
        var account = await host.RegisterAsync();
        await AssertWakeAsync(signal);

        Guid userId;
        using (var scope = host.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var challenge = await context.EmailVerificationChallenges.AsNoTracking().SingleAsync();
            userId = challenge.UserId;
            Assert.Null(challenge.DeliveredAt);
            Assert.Equal(0, challenge.DeliveryAttempts);
        }

        using (var scope = host.Factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<EmailVerificationService>()
                .ResendAsync(userId, Guid.NewGuid(), CancellationToken.None);
            Assert.Equal(VerificationStatus.Accepted, result.Status);
        }

        await AssertWakeAsync(signal);
        await host.RequestAsync(account.Email!);
        await AssertWakeAsync(signal);
        Assert.NotNull(await host.LatestAsync(account.Email!));

        await host.RequestAsync(account.Email!);
        await host.RequestAsync("unknown@example.invalid");
        using var cancellation = new CancellationTokenSource();
        var waiting = signal.WaitAsync(cancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    private static async Task AssertWakeAsync(MailDeliverySignal signal)
    {
        await signal.WaitAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
