using System.Net;
using System.Text.Json;
using Dealership.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    public async Task PictureReadsUseTheirOwnBudgetBesideTheApiBudget()
    {
        using var factory = CreateFactory(SlowRefill);
        for (var request = 0; request < ApiRateLimiting.RequestsPerMinute; request++)
        {
            var response = await Send(factory, FirstClient, "/api/taxonomy");
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests,
            (await Send(factory, FirstClient, "/api/taxonomy")).Response.StatusCode);
        var picture = await Send(factory, FirstClient, $"/api/accounts/{Guid.NewGuid()}/picture");
        Assert.Equal(StatusCodes.Status401Unauthorized, picture.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalBudgetIncludesPreflightWhileHealthProbesRemainExempt()
    {
        using var factory = CreateFactory(SlowRefill);
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
    public async Task AddressBudgetRefillsSmoothlyInsteadOfWaitingForTheWindowEdge()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiRequestsPerWindow"] = "2",
            ["RateLimiting:WindowSeconds"] = "2"
        });
        for (var request = 0; request < 2; request++)
        {
            Assert.Equal(StatusCodes.Status200OK,
                (await Send(factory, FirstClient, "/api/taxonomy")).Response.StatusCode);
        }

        var rejected = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.Equal("1", rejected.Response.Headers.RetryAfter.ToString());

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var status = StatusCodes.Status429TooManyRequests;
        while (status == StatusCodes.Status429TooManyRequests)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            status = (await Send(factory, FirstClient, "/api/taxonomy")).Response.StatusCode;
        }

        Assert.Equal(StatusCodes.Status200OK, status);
    }
    [Fact]
    public async Task ConcurrentRequestsCannotExceedTheBudget()
    {
        using var factory = CreateFactory(SlowRefill);
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

    [Fact]
    public async Task TrustedProxyGivesEachForwardedVisitorItsOwnAnonymousBudget()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiRequestsPerWindow"] = "2",
            ["RateLimiting:WindowSeconds"] = "3600"
        });
        for (var request = 0; request < 2; request++)
        {
            Assert.Equal(StatusCodes.Status200OK,
                (await Send(factory, TrustedProxy, "/api/taxonomy", FirstClient)).Response.StatusCode);
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests,
            (await Send(factory, TrustedProxy, "/api/taxonomy", FirstClient)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK,
            (await Send(factory, TrustedProxy, "/api/taxonomy", SecondClient)).Response.StatusCode);
    }

    [Fact]
    public async Task ConfiguredBudgetsAndWindowReplaceTheDefaults()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiRequestsPerWindow"] = "2",
            ["RateLimiting:PictureReadsPerWindow"] = "3",
            ["RateLimiting:LoginAttemptsPerWindow"] = "1",
            ["RateLimiting:VerificationAttemptsPerWindow"] = "2",
            ["RateLimiting:PhotoTransferConcurrency"] = "2",
            ["RateLimiting:WindowSeconds"] = "120"
        });
        using var client = factory.CreateClient();

        for (var request = 0; request < 2; request++)
        {
            Assert.Equal(StatusCodes.Status200OK,
                (await Send(factory, FirstClient, "/api/taxonomy")).Response.StatusCode);
        }

        var rejected = await Send(factory, FirstClient, "/api/taxonomy");
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.True(int.TryParse(rejected.Response.Headers.RetryAfter, out var retrySeconds));
        Assert.InRange(retrySeconds, 1, 120);

        for (var request = 0; request < 3; request++)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized,
                (await Send(factory, FirstClient, $"/api/accounts/{Guid.NewGuid()}/picture")).Response.StatusCode);
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests,
            (await Send(factory, FirstClient, $"/api/accounts/{Guid.NewGuid()}/picture")).Response.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await SendLogin(factory, SecondClient)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await SendLogin(factory, SecondClient)).Response.StatusCode);
    }

    [Theory]
    [InlineData("ApiRequestsPerWindow", 0)]
    [InlineData("ApiRequestsPerWindow", 100001)]
    [InlineData("BearerRequestsPerWindow", 0)]
    [InlineData("BearerRequestsPerWindow", 100001)]
    [InlineData("UserReadBurst", 0)]
    [InlineData("UserReadBurst", 100001)]
    [InlineData("UserReadTokensPerSecond", 0)]
    [InlineData("UserReadTokensPerSecond", 100001)]
    [InlineData("UserWritesPerWindow", 0)]
    [InlineData("UserWritesPerWindow", 100001)]
    [InlineData("PictureReadsPerWindow", -1)]
    [InlineData("PictureReadsPerWindow", 100001)]
    [InlineData("LoginAttemptsPerWindow", 0)]
    [InlineData("LoginAttemptsPerWindow", 100001)]
    [InlineData("VerificationAttemptsPerWindow", 0)]
    [InlineData("VerificationAttemptsPerWindow", 100001)]
    [InlineData("PhotoTransferConcurrency", 0)]
    [InlineData("PhotoTransferConcurrency", 1001)]
    [InlineData("PhotoReadConcurrency", 0)]
    [InlineData("PhotoReadQueueLimit", -1)]
    [InlineData("PhotoReadQueueSeconds", 11)]
    [InlineData("PictureDownloadConcurrency", 0)]
    [InlineData("PictureDownloadConcurrency", 1001)]
    [InlineData("PictureDownloadQueueLimit", -1)]
    [InlineData("PictureDownloadQueueLimit", 1001)]
    [InlineData("PictureDownloadQueueSeconds", 0)]
    [InlineData("PictureDownloadQueueSeconds", 11)]
    [InlineData("WindowSeconds", 0)]
    [InlineData("WindowSeconds", 3601)]
    public void InvalidConfigurationFailsStartup(string property, int value)
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            [$"RateLimiting:{property}"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task PictureDownloadQueueAdmitsAFullPageBurstAndRejectsOnlyBeyondItsBound()
    {
        var settings = new RateLimitingSettings { PictureDownloadQueueSeconds = 10 };
        using var limiter = new QueuedDownloadRateLimiter(settings.PictureDownloadConcurrency,
            settings.PictureDownloadQueueLimit, TimeSpan.FromSeconds(settings.PictureDownloadQueueSeconds));
        using var held = limiter.AttemptAcquire(settings.PictureDownloadConcurrency);
        Assert.True(held.IsAcquired);
        var queued = Enumerable.Range(0, settings.PictureDownloadQueueLimit)
            .Select(_ => limiter.AcquireAsync().AsTask()).ToArray();

        Assert.Equal(settings.PictureDownloadQueueLimit, limiter.GetStatistics()!.CurrentQueuedCount);
        using var rejected = await limiter.AcquireAsync();
        Assert.False(rejected.IsAcquired);

        held.Dispose();
        foreach (var pending in queued)
        {
            using var admitted = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(admitted.IsAcquired);
        }

        Assert.Equal(0, limiter.GetStatistics()!.CurrentQueuedCount);
        Assert.Equal(settings.PictureDownloadConcurrency, limiter.GetStatistics()!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task PictureDownloadCancellationAndQueueDeadlineReleaseTheirSlots()
    {
        using var limiter = new QueuedDownloadRateLimiter(1, 1, TimeSpan.FromSeconds(1));
        using var held = limiter.AttemptAcquire();
        using var caller = new CancellationTokenSource();
        var cancelled = limiter.AcquireAsync(cancellationToken: caller.Token).AsTask();
        Assert.Equal(1, limiter.GetStatistics()!.CurrentQueuedCount);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(0, limiter.GetStatistics()!.CurrentQueuedCount);

        using var expired = await limiter.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(expired.IsAcquired);
        Assert.Equal(0, limiter.GetStatistics()!.CurrentQueuedCount);
        held.Dispose();
        using var recovered = await limiter.AcquireAsync();
        Assert.True(recovered.IsAcquired);
    }

    [Fact]
    public void AccountPictureDownloadsAndUploadsUseIndependentPolicies()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var downloads = Assert.Single(endpoints.OfType<RouteEndpoint>(), endpoint =>
            endpoint.RoutePattern.RawText == "/api/accounts/{id:guid}/picture" &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Get));
        var uploads = Assert.Single(endpoints.OfType<RouteEndpoint>(), endpoint =>
            endpoint.RoutePattern.RawText == "/api/accounts/me/picture/{pictureId:guid}" &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Put));

        Assert.Equal(ApiRateLimiting.PictureDownloadPolicy,
            downloads.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(ApiRateLimiting.PhotoTransferPolicy,
            uploads.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public async Task HeldDownloadPermitsLeaveUploadsAvailableAndQueueExpiryReturnsTruthfulRejection()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:PictureDownloadConcurrency"] = "1",
            ["RateLimiting:PictureDownloadQueueLimit"] = "1",
            ["RateLimiting:PictureDownloadQueueSeconds"] = "1",
            ["RateLimiting:PhotoTransferConcurrency"] = "1"
        });
        builder.Services.AddApiRateLimiting(builder.Configuration);
        await using var app = builder.Build();
        app.UseRateLimiter();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.MapGet("/download", async () =>
        {
            entered.TrySetResult();
            await release.Task;
            return Results.NoContent();
        }).RequireRateLimiting(ApiRateLimiting.PictureDownloadPolicy);
        app.MapPut("/upload", () => Results.NoContent()).RequireRateLimiting(ApiRateLimiting.PhotoTransferPolicy);
        await app.StartAsync();
        using var client = app.GetTestClient();
        var held = client.GetAsync("/download");

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var upload = await client.PutAsync("/upload", null);
            Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
            using var rejected = await client.GetAsync("/download").WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.True(rejected.Headers.CacheControl?.NoStore);
            Assert.Equal(TimeSpan.FromSeconds(1), rejected.Headers.RetryAfter?.Delta);
        }
        finally
        {
            release.TrySetResult();
            using var completed = await held.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void EveryPhotoViewQueuesWhileEveryPhotoUploadKeepsTheStrictTransferCap()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var photos = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.Contains("photos", StringComparison.Ordinal) &&
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
            .Select(endpoint => (Read: endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                    .Contains(HttpMethods.Get),
                Policy: endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName))
            .ToArray();

        Assert.Contains(photos, item => item.Read);
        Assert.Contains(photos, item => !item.Read);
        Assert.All(photos, item => Assert.Equal(
            item.Read ? ApiRateLimiting.PhotoReadPolicy : ApiRateLimiting.PhotoTransferPolicy, item.Policy));
    }

    // A one hour window refills too slowly to add a token while a test drains its burst.
    private static Dictionary<string, string?> SlowRefill => new() { ["RateLimiting:WindowSeconds"] = "3600" };

    internal static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? overrides = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Readiness may probe only this unreachable test target, never the named database.
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Password=unavailable;Timeout=1"
                });
                configuration.AddInMemoryCollection(overrides ?? []);
            });
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
