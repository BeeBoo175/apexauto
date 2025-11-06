using System.Net;
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
    public async Task GlobalBudgetReturnsRetryInformationWhenExhausted()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, FirstClient, "/api/taxonomy");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var rejected = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.Equal("no-store", rejected.Response.Headers.CacheControl.ToString());
        Assert.Equal("application/problem+json", rejected.Response.ContentType);
        Assert.True(int.TryParse(rejected.Response.Headers.RetryAfter, out var retrySeconds));
        Assert.InRange(retrySeconds, 1, (int)ApiRateLimiting.RequestWindow.TotalSeconds);

        var otherClient = await Send(factory, SecondClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status200OK, otherClient.Response.StatusCode);
    }

    [Fact]
    public async Task CorsPreflightConsumesTheGlobalBudget()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var origin = factory.Services.GetRequiredService<IConfiguration>()["APP_BASE_URL"];

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await factory.Server.SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(FirstClient);
                context.Request.Method = HttpMethods.Options;
                context.Request.Path = "/api/valuations";
                context.Request.Host = new HostString("localhost");
                context.Request.Headers.Origin = origin;
                context.Request.Headers.AccessControlRequestMethod = HttpMethods.Post;
            });

            Assert.Equal(StatusCodes.Status204NoContent, response.Response.StatusCode);
        }

        var rejected = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
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
    public async Task UntrustedForwardingAndMappedAddressesCannotBypassTheBudget()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, FirstClient, "/api/taxonomy", SecondClient);
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var rejected = await Send(factory, "::ffff:198.51.100.10", "/api/taxonomy", "198.51.100.12");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
    }

    [Fact]
    public async Task TrustedProxyPreservesIndependentClientBudgets()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, TrustedProxy, "/api/taxonomy", FirstClient);
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var rejected = await Send(factory, TrustedProxy, "/api/taxonomy", FirstClient);
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        var otherClient = await Send(factory, TrustedProxy, "/api/taxonomy", SecondClient);
        Assert.Equal(StatusCodes.Status200OK, otherClient.Response.StatusCode);
    }

    [Fact]
    public async Task HealthProbesStayAvailableAfterTheBudgetIsExhausted()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            await Send(factory, FirstClient, "/api/taxonomy");
        }

        var limited = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.Response.StatusCode);

        var probe = await Send(factory, FirstClient, "/health");
        Assert.Equal(StatusCodes.Status200OK, probe.Response.StatusCode);

        var readiness = await Send(factory, FirstClient, "/health/ready");
        Assert.Contains(readiness.Response.StatusCode,
            new[] { StatusCodes.Status200OK, StatusCodes.Status503ServiceUnavailable });
    }

    [Fact]
    public async Task HealthProbesDoNotConsumeTheBusinessRequestBudget()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var request = 0; request <= ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, FirstClient, "/health");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        var businessRequest = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status200OK, businessRequest.Response.StatusCode);
    }

    [Fact]
    public async Task PreflightAndTaxonomyShareOneBudgetAcrossRoutes()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var origin = factory.Services.GetRequiredService<IConfiguration>()["APP_BASE_URL"];

        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute - 1; request++)
        {
            var response = await Send(factory, FirstClient, "/api/taxonomy");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

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
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                    options.KnownProxies.Add(IPAddress.Parse(TrustedProxy));
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
