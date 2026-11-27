using System.Net;
using System.Text;
using System.Text.Json;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.UnitTests;

public sealed class AppsScriptMailTransportTests
{
    private const string ScriptUrl = "https://script.google.com/macros/s/example-deployment/exec";

    private const string ScriptSecret = "example-script-secret-0123456789abcdef";

    [Fact]
    public async Task SendPostsTheWholeMessageAndAcceptsAConfirmedReply()
    {
        var handler = new ScriptHandler(HttpStatusCode.OK, """{"status":"sent"}""");

        await Transport(Settings(), handler).SendAsync(
            new OutboundMail("buyer@example.invalid", "Subject", "Plain body", "<p>Html body</p>"),
            CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Body!);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(ScriptUrl, handler.Address!.AbsoluteUri);
        Assert.Equal(ScriptSecret, sent.RootElement.GetProperty("secret").GetString());
        Assert.Equal("buyer@example.invalid", sent.RootElement.GetProperty("recipient").GetString());
        Assert.Equal("Subject", sent.RootElement.GetProperty("subject").GetString());
        Assert.Equal("Plain body", sent.RootElement.GetProperty("body").GetString());
        Assert.Equal("<p>Html body</p>", sent.RootElement.GetProperty("htmlBody").GetString());
        Assert.Equal("ApexAuto", sent.RootElement.GetProperty("senderName").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"status":"failed"}""")]
    [InlineData(HttpStatusCode.OK, """{"status":"rejected"}""")]
    [InlineData(HttpStatusCode.OK, "<html>Sign in</html>")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.InternalServerError, """{"status":"sent"}""")]
    public async Task SendWithoutAConfirmedReplyFailsSoTheMessageStaysQueued(HttpStatusCode status, string reply)
    {
        var transport = Transport(Settings(), new ScriptHandler(status, reply));

        await Assert.ThrowsAnyAsync<Exception>(() => transport.SendAsync(
            new OutboundMail("buyer@example.invalid", "Subject", "Body"), CancellationToken.None));
    }

    [Theory]
    [InlineData(null, ScriptSecret)]
    [InlineData("http://script.google.com/macros/s/example/exec", ScriptSecret)]
    [InlineData("https://script.google.com.example.invalid/macros/s/example/exec", ScriptSecret)]
    [InlineData("https://script.google.com:8443/macros/s/example/exec", ScriptSecret)]
    [InlineData("https://script.google.com/macros/s/example/dev", ScriptSecret)]
    [InlineData(ScriptUrl, null)]
    [InlineData(ScriptUrl, "too-short")]
    public void UnsafeOrIncompleteConfigurationIsRejected(string? url, string? secret)
    {
        var settings = Settings();
        settings.ScriptUrl = url;
        settings.ScriptSecret = secret;

        Assert.Throws<InvalidOperationException>(Transport(settings,
            new ScriptHandler(HttpStatusCode.OK, string.Empty)).Validate);
    }

    [Theory]
    [InlineData("AppsScript", typeof(AppsScriptMailTransport))]
    [InlineData("Smtp", typeof(SmtpMailTransport))]
    [InlineData("Loopback", typeof(SmtpMailTransport))]
    [InlineData("Disabled", typeof(SmtpMailTransport))]
    public void EmailModeSelectsTheTransport(string mode, Type expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Email:Mode"] = mode }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Production" });
        services.AddMailTransport(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsType(expected, provider.GetRequiredService<IMailTransport>());
    }

    private static EmailDeliverySettings Settings() => new()
    {
        Mode = AppsScriptMailTransport.ModeName,
        ScriptUrl = ScriptUrl,
        ScriptSecret = ScriptSecret
    };

    private static AppsScriptMailTransport Transport(EmailDeliverySettings settings, HttpMessageHandler handler) =>
        new(Options.Create(settings), new SingleClientFactory(handler));

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ScriptHandler(HttpStatusCode status, string reply) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? Address { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Address = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(reply, Encoding.UTF8,
                    reply.StartsWith('{') ? "application/json" : "text/html")
            };
        }
    }
}
