using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure.Identity;

public sealed class VerificationMailWorker(IServiceScopeFactory scopes, IVerificationMailSender sender,
    TimeProvider clock, ILogger<VerificationMailWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromSeconds(30);
    private static readonly Action<ILogger, string, Exception?> ClaimFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(1, nameof(ClaimFailure)),
        "Verification mail claim failed ({FailureType}); the worker will retry.");
    private static readonly Action<ILogger, string, Exception?> DeliveryFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2, nameof(DeliveryFailure)),
        "Verification mail delivery is unconfirmed ({FailureType}); the intent remains retryable.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sender.Enabled)
        {
            return;
        }

        sender.Validate();

        while (!stoppingToken.IsCancellationRequested)
        {
            for (var handled = 0; handled < 10; handled++)
            {
                EmailVerificationChallenge? challenge;
                Guid claimId;

                try
                {
                    (challenge, claimId) = await ClaimNextAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    ClaimFailure(logger, exception.GetType().Name, null);
                    break;
                }

                if (challenge is null)
                {
                    break;
                }

                try
                {
                    await sender.SendAsync(challenge, stoppingToken);
                    await MarkDeliveredAsync(challenge.Id, claimId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    DeliveryFailure(logger, exception.GetType().Name, null);
                }
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<(EmailVerificationChallenge?, Guid)> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var challenge = await database.EmailVerificationChallenges.FromSqlInterpolated($"""
            SELECT * FROM "EmailVerificationChallenges"
            WHERE "DeliveredAt" IS NULL AND "InvalidatedAt" IS NULL AND "ConsumedAt" IS NULL
              AND "ExpiresAt" > {now} AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "CreatedAt"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return (null, Guid.Empty);
        }

        var claimId = Guid.NewGuid();
        challenge.DeliveryClaimId = claimId;
        challenge.DeliveryClaimExpiresAt = now + ClaimLifetime;
        challenge.DeliveryAttempts++;
        challenge.NextDeliveryAt = now + RetryDelay(challenge.DeliveryAttempts);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (challenge, claimId);
    }

    private async Task MarkDeliveredAsync(Guid challengeId, Guid claimId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.Id == challengeId && challenge.DeliveryClaimId == claimId &&
                challenge.DeliveredAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.DeliveredAt, clock.GetUtcNow()),
                cancellationToken);
    }

    private static TimeSpan RetryDelay(int attempts)
    {
        return TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, Math.Min(attempts - 1, 5))));
    }
}
