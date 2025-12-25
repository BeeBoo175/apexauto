namespace Dealership.Infrastructure.Mail;

public sealed record OutboundMail(string Recipient, string Subject, string Body);

public interface IMailTransport
{
    bool Enabled { get; }

    void Validate();

    Task SendAsync(OutboundMail message, CancellationToken cancellationToken);
}
