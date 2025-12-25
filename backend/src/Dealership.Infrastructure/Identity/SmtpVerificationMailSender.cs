using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

public interface IVerificationMailSender
{
    bool Enabled { get; }

    void Validate();

    Task SendAsync(EmailVerificationChallenge challenge, CancellationToken cancellationToken);
}

public sealed class SmtpVerificationMailSender(IMailTransport transport, VerificationMailComposer composer)
    : IVerificationMailSender
{
    public bool Enabled => transport.Enabled;

    public void Validate()
    {
        transport.Validate();
    }

    public Task SendAsync(EmailVerificationChallenge challenge, CancellationToken cancellationToken)
    {
        return transport.SendAsync(composer.Compose(challenge), cancellationToken);
    }
}
