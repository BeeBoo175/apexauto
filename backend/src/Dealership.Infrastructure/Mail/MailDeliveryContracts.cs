namespace Dealership.Infrastructure.Mail;

public sealed record OutboundMail(string Recipient, string Subject, string Body, string? HtmlBody = null);

public sealed record ClaimedMail(Guid Id, Guid ClaimId, OutboundMail Message);

public interface IMailTransport
{
    bool Enabled { get; }

    void Validate();

    Task SendAsync(OutboundMail message, CancellationToken cancellationToken);
}

public interface IMailDeliverySource
{
    Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken);

    Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken);
}
