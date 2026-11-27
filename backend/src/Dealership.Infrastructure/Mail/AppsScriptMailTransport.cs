using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Mail;

// Sends through a Google Apps Script web app over HTTPS, for hosts that block outbound SMTP ports.
public sealed class AppsScriptMailTransport(IOptions<EmailDeliverySettings> options, IHttpClientFactory clients)
    : IMailTransport
{
    public const string ModeName = "AppsScript";

    public const string ClientName = "apps-script-mail";

    private const int MINIMUM_SECRET_LENGTH = 32;

    // Stays inside the delivery claim lease so a slow send is not claimed again while it runs.
    private const int SEND_DEADLINE_SECONDS = 20;

    public bool Enabled => options.Value.Mode != "Disabled";

    public void Validate()
    {
        var settings = options.Value;

        // The address check keeps the shared secret from being posted anywhere but Google's script host.
        if (settings.Mode != ModeName ||
            !Uri.TryCreate(settings.ScriptUrl, UriKind.Absolute, out var address) ||
            address.Scheme != Uri.UriSchemeHttps || address.Host != "script.google.com" || !address.IsDefaultPort ||
            !address.AbsolutePath.EndsWith("/exec", StringComparison.Ordinal) ||
            settings.ScriptSecret is not { Length: >= MINIMUM_SECRET_LENGTH })
        {
            throw new InvalidOperationException("Apps Script email delivery configuration is invalid.");
        }
    }

    public async Task SendAsync(OutboundMail message, CancellationToken cancellationToken)
    {
        Validate();
        var settings = options.Value;
        var request = new ScriptRequest(settings.ScriptSecret!, message.Recipient, message.Subject, message.Body,
            message.HtmlBody, settings.SenderDisplayName);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(SEND_DEADLINE_SECONDS));
        using var client = clients.CreateClient(ClientName);
        using var response = await client.PostAsJsonAsync(settings.ScriptUrl, request, deadline.Token);
        response.EnsureSuccessStatusCode();

        // A script cannot choose its HTTP status, so only an explicit reply confirms the send.
        if (await ReadStatusAsync(response, deadline.Token) != "sent")
        {
            throw new InvalidOperationException("Apps Script did not confirm the email was sent.");
        }
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var reply = await response.Content.ReadFromJsonAsync<ScriptReply>(cancellationToken);
            return reply?.Status;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // A misconfigured deployment answers with a Google sign-in or error page.
            return null;
        }
    }

    private sealed record ScriptRequest(string Secret, string Recipient, string Subject, string Body, string? HtmlBody,
        string SenderName);

    private sealed record ScriptReply(string? Status);
}
