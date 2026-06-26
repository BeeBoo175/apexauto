using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dealership.Api.Configuration;

public static class ApiRateLimiting
{
    public const int RequestsPerMinute = 100;
    public const int PictureReadsPerMinute = 300;
    public const int LoginAttemptsPerMinute = 5;
    public const string LoginPolicy = "login";
    public const string PhotoTransferPolicy = "photo-transfers";
    public const string VerificationPolicy = "email-verification";

    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter(PhotoTransferPolicy, limiter =>
            {
                limiter.PermitLimit = 4;
                limiter.QueueLimit = 0;
            });
            options.AddPolicy(LoginPolicy, context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(GetClientAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginAttemptsPerMinute,
                    Window = RequestWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.AddPolicy(VerificationPolicy, context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(GetClientAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = RequestWindow,
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
                    PermitLimit = pictures ? PictureReadsPerMinute : RequestsPerMinute,
                    Window = RequestWindow,
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
