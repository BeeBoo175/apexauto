using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.IntegrationTests;

public sealed class SmtpVerificationMailSenderTests
{
    [Fact]
    public async Task LoopbackSmtpReceivesVerificationLinkWithoutQueryToken()
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
        var sender = new SmtpVerificationMailSender(settings, new DevelopmentEnvironment(), configuration, tokens);
        var challenge = new EmailVerificationChallenge
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RecipientEmail = "nur.aina@example.invalid",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await sender.SendAsync(challenge, CancellationToken.None);
        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        var body = DecodeBody(message);
        var encoding = message.Split("\r\n", StringSplitOptions.None)
            .FirstOrDefault(line => line.StartsWith("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase));
        Assert.True(body.Contains($"/verify-email#token={tokens.Create(challenge)}", StringComparison.Ordinal),
            $"The SMTP body did not contain the verification fragment link. Encoding={encoding}, " +
            $"length={body.Length}, route={body.Contains("verify-email", StringComparison.Ordinal)}, " +
            $"fragment={body.Contains("#token=", StringComparison.Ordinal)}.");
        Assert.DoesNotContain("/verify-email?token=", body);
        Assert.Contains("nur.aina@example.invalid", message);
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
