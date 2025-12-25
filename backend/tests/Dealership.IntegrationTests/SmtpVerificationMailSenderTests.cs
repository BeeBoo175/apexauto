using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.IntegrationTests;

public sealed class SmtpVerificationMailSenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoopbackSmtpReceivesPurposeSpecificFragmentLink(bool passwordReset)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = ReceiveMessageAsync(listener);
        var jwt = Options.Create(new JwtSettings
        {
            Issuer = "apexauto-api",
            Audience = "apexauto-clients",
            ExpiryMinutes = 120,
            Secret = new string('s', 48)
        });
        var tokens = new EmailVerificationToken(jwt);
        var settings = Options.Create(new EmailDeliverySettings
        {
            Mode = "Loopback",
            SmtpHost = "127.0.0.1",
            SmtpPort = port,
            SenderEmail = "no-reply@apexauto.invalid"
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["APP_BASE_URL"] = "http://localhost:3000" }).Build();
        var environment = new DevelopmentEnvironment();
        var transport = new SmtpMailTransport(settings, environment);
        var links = new AuthMailLinks(configuration, settings, environment);
        var composer = new VerificationMailComposer(links, tokens);
        var challenge = new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RecipientEmail = "nur.aina@example.invalid",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        var route = "/verify-email";
        var expectedToken = tokens.Create(challenge);

        if (passwordReset)
        {
            var resetTokens = new PasswordResetToken(jwt);
            var resetComposer = new PasswordResetMailComposer(links, resetTokens);
            var resetChallenge = new PasswordResetChallenge
            {
                Id = challenge.Id,
                UserId = challenge.UserId,
                RecipientEmail = challenge.RecipientEmail,
                CreatedAt = challenge.CreatedAt,
                ExpiresAt = challenge.CreatedAt.AddHours(1)
            };
            expectedToken = resetTokens.Create(resetChallenge);
            route = "/reset-password";
            await transport.SendAsync(resetComposer.Compose(resetChallenge), CancellationToken.None);
        }
        else
        {
            await transport.SendAsync(composer.Compose(challenge), CancellationToken.None);
        }

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        var body = DecodeBody(message);
        var encoding = message.Split("\r\n", StringSplitOptions.None)
            .FirstOrDefault(line => line.StartsWith("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase));
        Assert.True(body.Contains($"{route}#token={expectedToken}", StringComparison.Ordinal),
            $"The SMTP body did not contain the expected fragment link. Encoding={encoding}, " +
            $"length={body.Length}, route={body.Contains(route, StringComparison.Ordinal)}, " +
            $"fragment={body.Contains("#token=", StringComparison.Ordinal)}.");
        Assert.DoesNotContain($"{route}?token=", body);
        Assert.Contains("nur.aina@example.invalid", message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledSmtpSendHonorsDeadlineAndCallerCancellation(bool cancelCaller)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var settings = Options.Create(new EmailDeliverySettings
        {
            Mode = "Loopback",
            SmtpHost = "127.0.0.1",
            SmtpPort = ((IPEndPoint)listener.LocalEndpoint).Port,
            SenderEmail = "noreply@example.invalid"
        });
        var transport = new SmtpMailTransport(settings, new DevelopmentEnvironment());
        using var cancellation = new CancellationTokenSource();
        var send = transport.SendAsync(new OutboundMail("nur.aina@example.invalid", "Account confirmation", "Link"),
            cancellation.Token);
        using var connected = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));

        if (cancelCaller)
        {
            await cancellation.CancelAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(TimeSpan.FromSeconds(15)));
    }

    private static async Task<string> ReceiveMessageAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
        using var writer = new StreamWriter(client.GetStream(), Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
        var message = new StringBuilder();
        await writer.WriteLineAsync("220 localhost ready");
        var readingData = false;

        while (await reader.ReadLineAsync() is { } line)
        {
            if (readingData)
            {
                if (line == ".")
                {
                    readingData = false;
                    await writer.WriteLineAsync("250 accepted");
                    continue;
                }

                message.AppendLine(line);
                continue;
            }

            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 localhost");
                continue;
            }

            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                readingData = true;
                await writer.WriteLineAsync("354 end with dot");
                continue;
            }

            if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 goodbye");
                break;
            }

            await writer.WriteLineAsync("250 accepted");
        }

        return message.ToString();
    }

    private static string DecodeBody(string message)
    {
        var boundary = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.True(boundary >= 0, "The SMTP message did not contain a header/body boundary.");
        var headers = message[..boundary];
        var body = message[(boundary + 4)..];

        if (headers.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(body));
        }

        if (headers.Contains("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase))
        {
            var unfolded = body.Replace("=\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("=\n", string.Empty, StringComparison.Ordinal);
            return Regex.Replace(unfolded, "=([0-9A-Fa-f]{2})", match =>
                ((char)Convert.ToByte(match.Groups[1].Value, 16)).ToString());
        }

        return body;
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Dealership.IntegrationTests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
