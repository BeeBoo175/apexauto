using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dealership.Infrastructure.Mail;

public sealed class MailDeliveryWorker(IServiceScopeFactory scopes, IMailTransport transport,
    ILogger<MailDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly Action<ILogger, string, Exception?> DeliveryFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(1, nameof(DeliveryFailure)),
        "Mail delivery is unconfirmed ({FailureType}); the intent remains retryable.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!transport.Enabled)
        {
            return;
        }

        transport.Validate();

        while (!stoppingToken.IsCancellationRequested)
        {
            await DeliverBatchAsync(stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    public async Task DeliverBatchAsync(CancellationToken cancellationToken)
    {
        int sourceCount;

        await using (var scope = scopes.CreateAsyncScope())
        {
            sourceCount = scope.ServiceProvider.GetServices<IMailDeliverySource>().Count();
        }

        for (var round = 0; round < 10; round++)
        {
            var claimed = false;

            // Give every purpose a turn, including when another purpose has a backlog or fails.
            for (var sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                claimed |= await DeliverNextAsync(sourceIndex, cancellationToken);
            }

            if (!claimed)
            {
                return;
            }
        }
    }

    private async Task<bool> DeliverNextAsync(int sourceIndex, CancellationToken cancellationToken)
    {
        var claimed = false;

        try
        {
            // A rolled-back claim must not leave tracked changes for another delivery attempt to save.
            await using var scope = scopes.CreateAsyncScope();
            var queue = scope.ServiceProvider.GetServices<IMailDeliverySource>().ElementAt(sourceIndex);
            var mail = await queue.ClaimNextAsync(cancellationToken);

            if (mail is null)
            {
                return false;
            }

            claimed = true;
            await transport.SendAsync(mail.Message, cancellationToken);
            await queue.MarkDeliveredAsync(mail.Id, mail.ClaimId, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DeliveryFailure(logger, exception.GetType().Name, null);
            return claimed;
        }
    }
}
