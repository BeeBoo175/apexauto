using System.Net;
using System.Text.Json;
using Dealership.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class ApiRateLimitingTests
{
    private const string FirstClient = "198.51.100.10";
    private const string SecondClient = "198.51.100.11";
    private const string TrustedProxy = "10.1.0.5";

    [Fact]
    public async Task LoginBudgetCountsInvalidAttemptsAndSharesMappedAddressesWithoutTrustingSpoofing()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < ApiRateLimiting.LoginAttemptsPerMinute; attempt++)
        {
            var response = await SendLogin(factory, attempt % 2 == 0 ? FirstClient : $"::ffff:{FirstClient}");
            Assert.Equal(StatusCodes.Status401Unauthorized, response.Response.StatusCode);
        }

        var rejected = await SendLogin(factory, FirstClient);
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.Equal("no-store", rejected.Response.Headers.CacheControl.ToString());
        Assert.True(int.TryParse(rejected.Response.Headers.RetryAfter, out var retrySeconds));
        Assert.InRange(retrySeconds, 1, (int)ApiRateLimiting.RequestWindow.TotalSeconds);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await SendLogin(factory, SecondClient)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Send(factory, FirstClient, "/api/taxonomy")).Response.StatusCode);
    }

    private static Task<HttpContext> SendLogin(WebApplicationFactory<Program> factory, string sender, string? forwardedClient = null)
    {
        return factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/api/auth/login";
            context.Request.Host = new HostString("localhost");
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));
            context.Request.Headers["X-Forwarded-For"] = forwardedClient ?? $"198.51.100.{Random.Shared.Next(20, 200)}";
        });
    }

    [Fact]
    public async Task GlobalBudgetIncludesPreflightWhileHealthProbesRemainExempt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        using var healthJson = JsonDocument.Parse(await healthResponse.Content.ReadAsStringAsync());
        Assert.Equal("healthy", healthJson.RootElement.GetProperty("status").GetString());

        for (var request = 0; request <= ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, FirstClient, "/health");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var readinessBefore = await Send(factory, FirstClient, "/health/ready");
        Assert.Contains(readinessBefore.Response.StatusCode,
            new[] { StatusCodes.Status200OK, StatusCodes.Status503ServiceUnavailable });

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute - 1; request++)
        {
            var response = await Send(factory, FirstClient, "/api/taxonomy");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var origin = factory.Services.GetRequiredService<IConfiguration>()["APP_BASE_URL"];
        var preflight = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(FirstClient);
            context.Request.Method = HttpMethods.Options;
            context.Request.Path = "/api/valuations";
            context.Request.Host = new HostString("localhost");
            context.Request.Headers.Origin = origin;
            context.Request.Headers.AccessControlRequestMethod = HttpMethods.Post;
        });
        Assert.Equal(StatusCodes.Status204NoContent, preflight.Response.StatusCode);

        var rejected = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.Equal("no-store", rejected.Response.Headers.CacheControl.ToString());
        Assert.Equal("application/problem+json", rejected.Response.ContentType);
        Assert.True(int.TryParse(rejected.Response.Headers.RetryAfter, out var retrySeconds));
        Assert.InRange(retrySeconds, 1, (int)ApiRateLimiting.RequestWindow.TotalSeconds);

        var otherClient = await Send(factory, SecondClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status200OK, otherClient.Response.StatusCode);

        var healthAfter = await Send(factory, FirstClient, "/health");
        Assert.Equal(StatusCodes.Status200OK, healthAfter.Response.StatusCode);
        var readinessAfter = await Send(factory, FirstClient, "/health/ready");
        Assert.Contains(readinessAfter.Response.StatusCode,
            new[] { StatusCodes.Status200OK, StatusCodes.Status503ServiceUnavailable });
    }

    [Fact]
    public async Task ConcurrentRequestsCannotExceedTheBudget()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var requests = Enumerable.Range(0, ApiRateLimiting.RequestsPerMinute + 20)
            .Select(_ => Send(factory, FirstClient, "/api/taxonomy"));
        var responses = await Task.WhenAll(requests);

        Assert.Equal(ApiRateLimiting.RequestsPerMinute,
            responses.Count(response => response.Response.StatusCode == StatusCodes.Status200OK));
        Assert.All(responses, response => Assert.Contains(response.Response.StatusCode,
            new[] { StatusCodes.Status200OK, StatusCodes.Status429TooManyRequests }));
    }

    [Fact]
    public async Task TrustedProxyPreservesIndependentLoginBudgetsAndConsumesOnlyTheNearestAddress()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < ApiRateLimiting.LoginAttemptsPerMinute; attempt++)
        {
            var response = await SendLogin(factory, TrustedProxy, $"{SecondClient}, {FirstClient}");
            Assert.Equal(StatusCodes.Status401Unauthorized, response.Response.StatusCode);
        }

        var rejected = await SendLogin(factory, TrustedProxy, $"198.51.100.99, {FirstClient}");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        var otherClient = await SendLogin(factory, TrustedProxy, SecondClient);
        Assert.Equal(StatusCodes.Status401Unauthorized, otherClient.Response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Readiness may probe only this unreachable test target, never the named database.
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Password=unavailable;Timeout=1"
                }));
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<ForwardedHeadersOptions>(options =>
                {
                    var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                        new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = TrustedProxy }).Build();
                    HostConfiguration.ConfigureForwardedHeaders(options, configuration);
                });
            });
        });
    }

    private static Task<HttpContext> Send(WebApplicationFactory<Program> factory, string sender, string path, string? forwardedClient = null)
    {
        return factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            context.Request.Host = new HostString("localhost");

            if (forwardedClient is not null)
            {
                context.Request.Headers["X-Forwarded-For"] = forwardedClient;
            }
        });
    }
}
