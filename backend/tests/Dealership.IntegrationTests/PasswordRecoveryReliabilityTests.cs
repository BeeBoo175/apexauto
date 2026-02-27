using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class PasswordRecoveryReliabilityTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedReceiptRollsBackCredentialsRevocationsAndConsumptionBeforeSameScopeRetry(bool cancelled)
    {
        var interruption = new PasswordRecoveryInterruption();
        using var host = new PasswordRecoveryHost(database, interceptor: interruption);
        var account = await host.RegisterAsync();
        Assert.NotNull(await host.LoginAsync(account));
        await host.RequestAsync(account.Email!);
        var challenge = await host.LatestAsync(account.Email!);
        var token = host.Token(challenge);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>();
        var request = new PasswordResetRequest(token, "Recovered123!");
        var before = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        using var cancellation = new CancellationTokenSource();
        interruption.FailReceipt = true;
        interruption.ReceiptCancellation = cancelled ? cancellation : null;

        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ResetAsync(request, cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetAsync(request, cancellation.Token));
        }

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        var retained = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        Assert.Equal(before.PasswordHash, retained.PasswordHash);
        Assert.Equal(before.SecurityStamp, retained.SecurityStamp);
        Assert.Equal(before.ConcurrencyStamp, retained.ConcurrencyStamp);
        Assert.False(await context.AccountSessions.AnyAsync(session => session.UserId == challenge.UserId && session.RevokedAt != null));
        Assert.Null((await host.LatestAsync(account.Email!)).CompletedSecurityStamp);
        interruption.FailReceipt = false;
        Assert.NotNull(await host.LoginAsync(account));
        Assert.Null((await host.LatestAsync(account.Email!)).ConsumedAt);

        Assert.Equal(PasswordResetStatus.Succeeded, (await service.ResetAsync(request, CancellationToken.None)).Status);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.NotNull((await host.LatestAsync(account.Email!)).ConsumedAt);
    }

    [Fact]
    public async Task CurrentOrdinalRecipientAndExpiryGateFirstResetAndConsumedReplay()
    {
        var clock = new RecoveryClock();
        using var host = new PasswordRecoveryHost(database, clock);
        var account = await host.RegisterAsync();
        await host.RequestAsync(account.Email!);
        var challenge = await host.LatestAsync(account.Email!);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>();
        var request = new PasswordResetRequest(host.Token(challenge), "Recovered123!");

        // A stale tracked user must not hide a current recipient change under the account lock.
        await context.Users.SingleAsync(user => user.Id == challenge.UserId);
        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Email, account.Email!.ToUpperInvariant()));
        Assert.Equal(PasswordResetStatus.Invalid, (await service.ResetAsync(request, CancellationToken.None)).Status);
        Assert.Null((await host.LatestAsync(account.Email!)).ConsumedAt);
        await context.Users.Where(user => user.Id == challenge.UserId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Email, account.Email));
        Assert.Equal(PasswordResetStatus.Succeeded, (await service.ResetAsync(request, CancellationToken.None)).Status);
        var consumed = await host.LatestAsync(account.Email!);
        var credentials = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        clock.Now = challenge.ExpiresAt.AddTicks(-1);
        Assert.Equal(PasswordResetStatus.Succeeded, (await service.ResetAsync(request, CancellationToken.None)).Status);
        clock.Now = challenge.ExpiresAt;
        Assert.Equal(PasswordResetStatus.Invalid, (await service.ResetAsync(request, CancellationToken.None)).Status);
        Assert.Equal(consumed.ConsumedAt, (await host.LatestAsync(account.Email!)).ConsumedAt);
        var replayed = await context.Users.AsNoTracking().SingleAsync(user => user.Id == challenge.UserId);
        Assert.Equal(credentials.PasswordHash, replayed.PasswordHash);
        Assert.Equal(credentials.SecurityStamp, replayed.SecurityStamp);
        Assert.Equal(credentials.ConcurrencyStamp, replayed.ConcurrencyStamp);
    }

    [Fact]
    public async Task LoginWaitsForResetAndCompetingDifferentPasswordsCannotBothSucceed()
    {
        var pause = new PasswordRecoveryInterruption();
        using var host = new PasswordRecoveryHost(database, interceptor: pause);
        var account = await host.RegisterAsync();
        await host.RequestAsync(account.Email!);
        var token = host.Token(await host.LatestAsync(account.Email!));
        pause.PauseCredentials = true;
        var reset = host.ResetAsync(token);
        await pause.CredentialsReached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var login = host.LoginAsync(account);

        try
        {
            await WaitForLoginLockAsync();
            Assert.False(login.IsCompleted);
        }
        finally
        {
            pause.PauseCredentials = false;
            pause.Resume.TrySetResult();
            await reset;
            await login;
        }

        Assert.Equal(PasswordResetStatus.Succeeded, (await reset).Status);
        Assert.Null(await login);
        var contenders = await host.RegisterAsync();
        await host.RequestAsync(contenders.Email!);
        token = host.Token(await host.LatestAsync(contenders.Email!));
        var outcomes = await Task.WhenAll(host.ResetAsync(token, "FirstReset123!"), host.ResetAsync(token, "SecondReset123!"));
        Assert.Single(outcomes, outcome => outcome.Status == PasswordResetStatus.Succeeded);
        Assert.Single(outcomes, outcome => outcome.Status == PasswordResetStatus.Invalid);
        var winner = outcomes[0].Status == PasswordResetStatus.Succeeded ? "FirstReset123!" : "SecondReset123!";
        Assert.NotNull(await host.LoginAsync(contenders, winner));
    }

    [Fact]
    public async Task FailedDeliveryIsRetriedAfterRestartWithTheSameTokenAndPersistedIntent()
    {
        var clock = new RecoveryClock();
        RegistrationRequest account;
        PasswordResetChallenge challenge;
        var failed = new RecordingMailTransport(fail: true, subject: "Reset your ApexAuto password");

        using (var host = new PasswordRecoveryHost(database, clock, sender: failed))
        {
            account = await host.RegisterAsync();
            await host.RequestAsync(account.Email!);
            challenge = await host.LatestAsync(account.Email!);
            var attempted = await failed.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Contains(host.Token(challenge), attempted.Body);
        }

        clock.Now += TimeSpan.FromMinutes(2);
        var delivered = new RecordingMailTransport(fail: false, subject: "Reset your ApexAuto password");
        using var restarted = new PasswordRecoveryHost(database, clock, sender: delivered);
        var message = await delivered.Attempt.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains(restarted.Token(challenge), message.Body);
        Assert.Equal(challenge.Id, (await restarted.LatestAsync(account.Email!)).Id);
        Assert.Equal(challenge.ExpiresAt, (await restarted.LatestAsync(account.Email!)).ExpiresAt);
        await WaitForDeliveryAsync(restarted, account.Email!);
        Assert.Equal(PasswordResetStatus.Succeeded, (await restarted.ResetAsync(restarted.Token(challenge))).Status);
    }

    private async Task WaitForLoginLockAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND pid <> pg_backend_pid()
                  AND wait_event_type = 'Lock' AND query LIKE '%NormalizedEmail%FOR UPDATE%')
            """, connection);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await query.ExecuteScalarAsync() is true)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("Old-password login did not wait on the reset's account-row lock.");
    }

    private static async Task WaitForDeliveryAsync(PasswordRecoveryHost host, string email)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var persisted = await host.LatestAsync(email);

            if (persisted.DeliveredAt is not null)
            {
                Assert.Equal(2, persisted.DeliveryAttempts);
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("Restarted reset worker did not confirm delivery.");
    }

}
