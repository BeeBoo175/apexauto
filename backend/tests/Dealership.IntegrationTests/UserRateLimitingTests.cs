using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Dealership.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class UserRateLimitingTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task PersistedUsersBehindOneAddressHaveIndependentReadsAndWrites()
    {
        using var factory = fixture.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RateLimiting:UserReadBurst"] = "2",
                    ["RateLimiting:UserReadTokensPerSecond"] = "1",
                    ["RateLimiting:UserWritesPerWindow"] = "2"
                })));
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        using var anotherSession = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(first);
        await IntakePhotoFixture.RegisterAsync(second);
        await IntakePhotoFixture.LoginAsync(anotherSession, registration);
        var firstToken = first.DefaultRequestHeaders.Authorization!.ToString();
        var secondToken = second.DefaultRequestHeaders.Authorization!.ToString();

        for (var request = 0; request < 2; request++)
        {
            Assert.Equal(StatusCodes.Status200OK,
                (await Send(factory, firstToken, "/api/auth/session")).Response.StatusCode);
        }

        AssertRejected(await Send(factory, firstToken, "/api/auth/session"), 1);
        AssertRejected(await Send(factory, anotherSession.DefaultRequestHeaders.Authorization!.ToString(),
            "/api/auth/session", "198.51.100.11"), 1);
        Assert.Equal(StatusCodes.Status200OK,
            (await Send(factory, secondToken, "/api/auth/session")).Response.StatusCode);
        AssertRejected(await Send(factory, firstToken, "/api/taxonomy", method: HttpMethods.Head), 1);

        for (var request = 0; request < 2; request++)
        {
            Assert.Equal(StatusCodes.Status400BadRequest,
                (await Send(factory, firstToken, "/api/account/username", method: HttpMethods.Put)).Response.StatusCode);
        }

        AssertRejected(await Send(factory, firstToken, "/api/account/username", method: HttpMethods.Put), 30);
        Assert.Equal(StatusCodes.Status400BadRequest,
            (await Send(factory, secondToken, "/api/account/username", method: HttpMethods.Put)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Send(factory, firstToken, "/health")).Response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer forged")]
    [InlineData("bearer invalid.signature.token")]
    public async Task InvalidBearerSpendsAFiniteAddressCeilingWithoutCreatingUserPartitions(string bearer)
    {
        using var factory = ApiRateLimitingTests.CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiRequestsPerWindow"] = "1",
            ["RateLimiting:BearerRequestsPerWindow"] = "3"
        });
        using var client = factory.CreateClient();

        for (var request = 0; request < 3; request++)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized,
                (await Send(factory, $"{bearer}{request}", "/api/auth/session")).Response.StatusCode);
        }

        AssertRejected(await Send(factory, bearer, "/api/auth/session"), 20);
        Assert.Equal(StatusCodes.Status200OK,
            (await Send(factory, bearer, "/health")).Response.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized,
            (await Send(factory, bearer, "/api/auth/session", "198.51.100.11")).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await Send(factory, null, "/api/taxonomy")).Response.StatusCode);
        AssertRejected(await Send(factory, null, "/api/taxonomy"), 60);
    }

    [Fact]
    public async Task ReadBucketRefillsWhileWritesAndHealthKeepTheirOwnAdmission()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:UserReadBurst"] = "1",
            ["RateLimiting:UserReadTokensPerSecond"] = "1",
            ["RateLimiting:UserWritesPerWindow"] = "1"
        });
        builder.Services.AddApiRateLimiting(builder.Configuration);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, "7c686e64-935c-4589-a109-8680ae4ed2af")], "validated"));
            await next(context);
        });
        app.UseMiddleware<UserRateLimiting>();
        app.MapMethods("/resource", [HttpMethods.Get, HttpMethods.Head, HttpMethods.Put], () => Results.NoContent());
        app.MapGet("/health", () => Results.NoContent()).DisableRateLimiting();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var admitted = await client.GetAsync("/resource");
        Assert.Equal(HttpStatusCode.NoContent, admitted.StatusCode);
        using var rejected = await client.GetAsync("/resource");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), rejected.Headers.RetryAfter?.Delta);
        Assert.True(rejected.Headers.CacheControl?.NoStore);
        using var write = await client.PutAsync("/resource", null);
        Assert.Equal(HttpStatusCode.NoContent, write.StatusCode);
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.NoContent, health.StatusCode);

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        HttpStatusCode status;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            using var recovered = await client.GetAsync("/resource", deadline.Token);
            status = recovered.StatusCode;
        }
        while (status == HttpStatusCode.TooManyRequests);

        Assert.Equal(HttpStatusCode.NoContent, status);
    }

    private static void AssertRejected(HttpContext context, int maximumRetrySeconds)
    {
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.True(int.TryParse(context.Response.Headers.RetryAfter, out var seconds));
        Assert.InRange(seconds, 1, maximumRetrySeconds);
    }

    private static Task<HttpContext> Send(WebApplicationFactory<Program> factory, string? bearer, string path,
        string sender = "198.51.100.10", string method = "GET")
    {
        return factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            context.Request.Method = method;
            context.Request.Path = path;
            context.Request.Host = new HostString("localhost");
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}"));

            if (bearer is not null)
            {
                context.Request.Headers.Authorization = bearer;
            }
        });
    }
}
