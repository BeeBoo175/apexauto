using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dealership.Api.Configuration;

public static class ApiRateLimiting
{
    public const int RequestsPerMinute = 300;
    public const int PictureReadsPerMinute = 300;
    public const int LoginAttemptsPerMinute = 5;
    public const string LoginPolicy = "login";
    public const string PhotoTransferPolicy = "photo-transfers";
    public const string PhotoReadPolicy = "photo-reads";
    public const string PictureDownloadPolicy = "picture-downloads";
    public const string VerificationPolicy = "email-verification";

    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .Validate(settings => settings.ApiRequestsPerWindow is > 0 and <= 100000 &&
                settings.BearerRequestsPerWindow is > 0 and <= 100000 &&
                settings.UserReadBurst is > 0 and <= 100000 &&
                settings.UserReadTokensPerSecond is > 0 and <= 100000 &&
                settings.UserWritesPerWindow is > 0 and <= 100000 &&
                settings.PictureReadsPerWindow is > 0 and <= 100000 &&
                settings.LoginAttemptsPerWindow is > 0 and <= 100000 &&
                settings.VerificationAttemptsPerWindow is > 0 and <= 100000,
                "Rate limiting request limits must be between 1 and 100000.")
            .Validate(settings => settings.PhotoTransferConcurrency is > 0 and <= 1000,
                "Photo transfer concurrency must be between 1 and 1000.")
            .Validate(settings => settings.PictureDownloadConcurrency is > 0 and <= 1000 &&
                settings.PictureDownloadQueueLimit is >= 0 and <= 1000 &&
                settings.PictureDownloadQueueSeconds is >= 1 and <= 10,
                "Picture download concurrency must be between 1 and 1000, queue size between 0 and 1000, and queue wait between 1 and 10 seconds.")
            .Validate(settings => settings.PhotoReadConcurrency is > 0 and <= 1000 &&
                settings.PhotoReadQueueLimit is >= 0 and <= 1000 &&
                settings.PhotoReadQueueSeconds is >= 1 and <= 10,
                "Photo read concurrency must be between 1 and 1000, queue size between 0 and 1000, and queue wait between 1 and 10 seconds.")
            .Validate(settings => settings.WindowSeconds is >= 1 and <= 3600,
                "Rate limiting window must be between 1 and 3600 seconds.")
            .ValidateOnStart();
        services.AddSingleton<UserRateLimiting>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>>((options, configured) =>
        {
            var settings = configured.Value;
            var window = TimeSpan.FromSeconds(settings.WindowSeconds);
            options.AddPolicy(PictureDownloadPolicy, _ =>
                RateLimitPartition.Get(PictureDownloadPolicy, _ => new QueuedDownloadRateLimiter(
                    settings.PictureDownloadConcurrency, settings.PictureDownloadQueueLimit,
                    TimeSpan.FromSeconds(settings.PictureDownloadQueueSeconds))));
            options.AddPolicy(PhotoReadPolicy, _ =>
                RateLimitPartition.Get(PhotoReadPolicy, _ => new QueuedDownloadRateLimiter(
                    settings.PhotoReadConcurrency, settings.PhotoReadQueueLimit,
                    TimeSpan.FromSeconds(settings.PhotoReadQueueSeconds))));
            options.AddConcurrencyLimiter(PhotoTransferPolicy, limiter =>
            {
                limiter.PermitLimit = settings.PhotoTransferConcurrency;
                limiter.QueueLimit = 0;
            });
            options.AddPolicy(LoginPolicy, context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(GetClientAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.LoginAttemptsPerWindow,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.AddPolicy(VerificationPolicy, context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(GetClientAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.VerificationAttemptsPerWindow,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Private pictures retain a separate address budget from API requests.
                var pictures = IsPictureRead(context);
                var bearer = context.Request.Headers.Authorization.ToString()
                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                var budget = bearer ? settings.BearerRequestsPerWindow : settings.ApiRequestsPerWindow;
                var prefix = bearer ? "bearer:" : "api:";
                var partition = (pictures ? "pictures:" : prefix) + GetClientAddress(context);
                return RateLimitPartition.GetTokenBucketLimiter(
                    partition,
                    _ => SmoothBudget(pictures ? settings.PictureReadsPerWindow : budget, window));
            });
            options.OnRejected = (context, _) => new ValueTask(RejectAsync(context.HttpContext, context.Lease));
        });

        return services;
    }

    // One smooth address budget: a burst of `budget` that refills evenly across the window, so a
    // short burst never costs a full dead window and the window edge has no cliff.
    internal static TokenBucketRateLimiterOptions SmoothBudget(int budget, TimeSpan window)
    {
        var period = TimeSpan.FromSeconds(Math.Max(0.1, window.TotalSeconds / budget));
        var tokens = Math.Max(1, (int)Math.Round(budget * period.TotalSeconds / window.TotalSeconds));
        return new TokenBucketRateLimiterOptions
        {
            TokenLimit = budget,
            TokensPerPeriod = tokens,
            ReplenishmentPeriod = period,
            AutoReplenishment = true,
            // Reject immediately so bursts do not accumulate pending requests.
            QueueLimit = 0
        };
    }
    internal static Task RejectAsync(HttpContext context, RateLimitLease lease)
    {
        var seconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Request limit reached.").ExecuteAsync(context);
    }

    private static bool IsPictureRead(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return HttpMethods.IsGet(context.Request.Method) &&
            path.StartsWith("/api/accounts/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith("/picture", StringComparison.OrdinalIgnoreCase);
    }

    private static IPAddress GetClientAddress(HttpContext context)
    {
        // Missing sockets share a conservative partition; mapped IPv6 shares its IPv4 budget.
        var address = context.Connection.RemoteIpAddress ?? IPAddress.None;
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
