using System.Net;
using System.Net.Mail;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Mail;

public sealed class SmtpMailTransport(IOptions<EmailDeliverySettings> options, IHostEnvironment environment) : IMailTransport
{
    public bool Enabled => options.Value.Mode != "Disabled";

    public void Validate()
    {
        var settings = options.Value;

        if (settings.Mode == "Disabled")
        {
            return;
        }

        if (settings.Mode is not ("Loopback" or "Smtp") || string.IsNullOrWhiteSpace(settings.SmtpHost) ||
            settings.SmtpPort is < 1 or > 65535 || string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            throw new InvalidOperationException("Email delivery configuration is invalid.");
        }

        if (settings.Mode == "Loopback" && (!environment.IsDevelopment() ||
            settings.SmtpHost is not ("localhost" or "127.0.0.1" or "::1")))
        {
            throw new InvalidOperationException("Loopback email delivery requires Development and local hosts.");
        }

        if (settings.Mode == "Smtp" && (string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.AppPassword) || !settings.EnableSsl))
        {
            throw new InvalidOperationException("SMTP delivery requires private credentials and TLS.");
        }
    }

    public async Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
    {
        Validate();
        var settings = options.Value;

        if (!Enabled)
        {
            throw new InvalidOperationException("Email delivery is disabled.");
        }

        using var smtpMessage = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail!, settings.SenderDisplayName),
            Subject = message.Subject,
            Body = message.Body
        };
        smtpMessage.To.Add(message.Recipient);
        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = settings.Mode == "Smtp"
                ? new NetworkCredential(settings.Username, settings.AppPassword)
                : null
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await client.SendMailAsync(smtpMessage, deadline.Token);
    }
}
