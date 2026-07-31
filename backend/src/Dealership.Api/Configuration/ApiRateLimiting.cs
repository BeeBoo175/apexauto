using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dealership.Api.Configuration;

public static class ApiRateLimiting
{
    public const int RequestsPerMinute = 100;
    public const int PictureReadsPerMinute = 300;
    public const int LoginAttemptsPerMinute = 5;
    public const string LoginPolicy = "login";
    public const string PhotoTransferPolicy = "photo-transfers";
    public const string PictureDownloadPolicy = "picture-downloads";
    public const string VerificationPolicy = "email-verification";

    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .Validate(settings => settings.ApiRequestsPerWindow is > 0 and <= 100000 &&
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
            .Validate(settings => settings.WindowSeconds is >= 1 and <= 3600,
                "Rate limiting window must be between 1 and 3600 seconds.")
            .ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>>((options, configured) =>
        {
            var settings = configured.Value;
            var window = TimeSpan.FromSeconds(settings.WindowSeconds);
            options.AddPolicy(PictureDownloadPolicy, _ =>
                RateLimitPartition.Get(PictureDownloadPolicy, _ => new PictureDownloadRateLimiter(settings)));
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
                // A page of avatars reads many small private pictures; they draw on their own budget so they
                // cannot starve the API calls of the same page.
                var pictures = IsPictureRead(context);
                var partition = (pictures ? "pictures:" : "api:") + GetClientAddress(context);
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = pictures ? settings.PictureReadsPerWindow : settings.ApiRequestsPerWindow,
                    Window = window,
                    // Reject immediately so bursts do not accumulate pending requests.
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    // Round up remaining lease duration to guarantee a strictly positive integer seconds interval.
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    context.HttpContext.Response.Headers.RetryAfter = "1";
                }

                // Prevent intermediate proxies and edge caches from caching transient 429 rejections.
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Request limit reached.").ExecuteAsync(context.HttpContext);
            };
        });

        return services;
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
