using System.Net;
using System.Net.Mail;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Identity;

public interface IVerificationMailSender
{
    bool Enabled { get; }

    void Validate();

    Task SendAsync(EmailVerificationChallenge challenge, CancellationToken cancellationToken);
}

public sealed class SmtpVerificationMailSender(IOptions<EmailDeliverySettings> options, IHostEnvironment environment,
    IConfiguration configuration, EmailVerificationToken tokens) : IVerificationMailSender
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
            settings.SmtpPort is < 1 or > 65535 || string.IsNullOrWhiteSpace(settings.SenderEmail) ||
            !Uri.TryCreate(configuration["APP_BASE_URL"], UriKind.Absolute, out var origin))
        {
            throw new InvalidOperationException("Email delivery configuration is invalid.");
        }

        if (settings.Mode == "Loopback" && (!environment.IsDevelopment() ||
            settings.SmtpHost is not ("localhost" or "127.0.0.1" or "::1") || origin.Host is not ("localhost" or "127.0.0.1")))
        {
            throw new InvalidOperationException("Loopback email delivery requires Development and local hosts.");
        }

        if (settings.Mode == "Smtp" && (string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.AppPassword) || !settings.EnableSsl))
        {
            throw new InvalidOperationException("SMTP delivery requires private credentials and TLS.");
        }
    }

    public async Task SendAsync(EmailVerificationChallenge challenge, CancellationToken cancellationToken)
    {
        Validate();
        var settings = options.Value;

        if (!Enabled)
        {
            throw new InvalidOperationException("Email delivery is disabled.");
        }

        var baseUrl = configuration["APP_BASE_URL"]!.TrimEnd('/');
        var link = $"{baseUrl}/verify-email#token={Uri.EscapeDataString(tokens.Create(challenge))}";
        using var message = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail!, settings.SenderDisplayName),
            Subject = "Verify your ApexAuto email",
            Body = $"Open this link and choose Verify email to confirm your address:\n\n{link}\n\nThis link expires in 24 hours. If you did not create this account, ignore this message."
        };
        message.To.Add(challenge.RecipientEmail);
        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = settings.Mode == "Smtp"
                ? new NetworkCredential(settings.Username, settings.AppPassword)
                : null
        };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await client.SendMailAsync(message, deadline.Token);
    }
}
