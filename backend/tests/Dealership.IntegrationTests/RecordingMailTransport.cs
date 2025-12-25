using Dealership.Infrastructure.Mail;

namespace Dealership.IntegrationTests;

internal sealed class RecordingMailTransport(bool fail, string? recipient = null, string? subject = null) : IMailTransport
{
    public bool Enabled => true;

    public TaskCompletionSource<OutboundMail> Attempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Validate()
    {
    }

    public Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
    {
        if ((recipient is not null && message.Recipient != recipient) ||
            (subject is not null && message.Subject != subject))
        {
            return Task.CompletedTask;
        }

        Attempt.TrySetResult(message);

        if (fail)
        {
            throw new IOException("Simulated local mail transport failure.");
        }

        return Task.CompletedTask;
    }
}
