using System.Net.Mail;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.BrowserAuthHarness;

internal sealed class RecoveryLoopbackMailTransport(IOptions<EmailDeliverySettings> settings,
    IHostEnvironment environment) : IMailTransport
{
    private readonly SmtpMailTransport _transport = new(settings, environment);

    public bool Enabled => _transport.Enabled;

    public void Validate()
    {
        if (settings.Value.Mode != "Loopback" || settings.Value.SmtpHost != "127.0.0.1" ||
            settings.Value.SmtpPort != 1025 || settings.Value.EnableSsl || !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Recovery browser mail requires Development loopback SMTP on port 1025.");
        }

        _transport.Validate();
    }

    public Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
    {
        Validate();

        if (!MailAddress.TryCreate(message.Recipient, out var recipient) ||
            !recipient.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Recovery browser mail permits only reserved .invalid recipients.");
        }

        return _transport.SendAsync(message, cancellationToken);
    }
}
