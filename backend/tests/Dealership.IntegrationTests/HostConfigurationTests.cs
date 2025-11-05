using System.Net;
using Dealership.Api.Configuration;
using Dealership.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Dealership.IntegrationTests;

public class HostConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("localhost;*")]
    [InlineData("localhost,api.example.com")]
    [InlineData("localhost;")]
    [InlineData("*.example.com")]
    public void UnrestrictedOrMalformedHostListsAreRejected(string? hosts)
    {
        Assert.Throws<InvalidOperationException>(() => HostConfiguration.ValidateAllowedHosts(hosts));
    }

    [Fact]
    public void ExplicitHostsAreAccepted()
    {
        HostConfiguration.ValidateAllowedHosts("localhost; api.example.com");
    }

    [Fact]
    public void UnspecifiedEnvironmentIsProduction()
    {
        Assert.Equal("Production", HostConfiguration.ResolveEnvironment(new ConfigurationBuilder().Build()));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void ConfiguredEnvironmentIsPreserved(string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["environment"] = environment }).Build();

        Assert.Equal(environment, HostConfiguration.ResolveEnvironment(configuration));
    }

    [Theory]
    [InlineData(null, "10.1.0.5", "http|10.1.0.5")]
    [InlineData("10.1.0.6", "10.1.0.5", "http|10.1.0.5")]
    [InlineData("10.1.0.5", "10.1.0.5", "https|203.0.113.9")]
    public async Task OnlyExplicitlyTrustedSendersCanForwardHeaders(string? trustedProxy, string sender, string expected)
    {
        var values = new Dictionary<string, string?>();

        if (trustedProxy != null)
        {
            values["ForwardedHeaders:KnownProxies:0"] = trustedProxy;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var options = new ForwardedHeadersOptions();
        HostConfiguration.ConfigureForwardedHeaders(options, configuration);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            await next(context);
        });
        app.UseForwardedHeaders(options);
        app.Run(context => context.Response.WriteAsync($"{context.Request.Scheme}|{context.Connection.RemoteIpAddress}"));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);

        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void InvalidProxyAddressIsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "not-an-address" }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            HostConfiguration.ConfigureForwardedHeaders(new ForwardedHeadersOptions(), configuration));
    }

    [Fact]
    public void MalformedDotEnvDiagnosticsDoNotExposeContents()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var envPath = Path.Combine(directory, ".env");
        File.WriteAllText(envPath, "# configuration\nprivate-secret-without-separator\n");

        try
        {
            var exception = Assert.Throws<FormatException>(() => DotEnvLoader.LoadForDevelopment(directory));
            Assert.Contains(envPath, exception.Message);
            Assert.Contains("line 2", exception.Message);
            Assert.DoesNotContain("private-secret", exception.Message);
        }
        finally
        {
            File.Delete(envPath);
            Directory.Delete(directory);
        }
    }
}
