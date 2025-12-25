using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dealership.UnitTests;

public sealed class MailConfigurationTests
{
    [Fact]
    public void OrdinaryTransportDoesNotRequireFrontendLinkConfiguration()
    {
        var settings = LocalSettings();
        var transport = new SmtpMailTransport(Options.Create(settings), new TestEnvironment());
        transport.Validate();
        Assert.True(transport.Enabled);
    }

    [Theory]
    [InlineData("Loopback", "smtp.example.invalid", 1025, false, "Development")]
    [InlineData("Loopback", "127.0.0.1", 1025, false, "Production")]
    [InlineData("Loopback", "127.0.0.1", 0, false, "Development")]
    [InlineData("Smtp", "smtp.example.invalid", 587, false, "Production")]
    [InlineData("Smtp", "smtp.example.invalid", 587, true, "Production")]
    [InlineData("Other", "127.0.0.1", 1025, false, "Development")]
    public void UnsafeOrInvalidTransportConfigurationIsRejected(string mode, string host, int port,
        bool ssl, string environment)
    {
        var settings = LocalSettings();
        settings.Mode = mode;
        settings.SmtpHost = host;
        settings.SmtpPort = port;
        settings.EnableSsl = ssl;
        var transport = new SmtpMailTransport(Options.Create(settings),
            new TestEnvironment { EnvironmentName = environment });

        Assert.Throws<InvalidOperationException>(transport.Validate);
    }

    [Theory]
    [InlineData(null, "Development")]
    [InlineData("relative/path", "Development")]
    [InlineData("https://apexauto.example.invalid", "Development")]
    [InlineData("http://localhost:3000", "Production")]
    public void AuthenticationCompositionRetainsFrontendOriginRestrictions(string? origin, string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["APP_BASE_URL"] = origin }).Build();

        Assert.Throws<InvalidOperationException>(() => new AuthMailLinks(configuration,
            Options.Create(LocalSettings()), new TestEnvironment { EnvironmentName = environment }));
    }

    private static EmailDeliverySettings LocalSettings() => new()
    {
        Mode = "Loopback",
        SmtpHost = "127.0.0.1",
        SmtpPort = 1025,
        SenderEmail = "noreply@example.invalid"
    };

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Dealership.UnitTests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
