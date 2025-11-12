using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

namespace Dealership.Api.Configuration;

public static class ApiRateLimiting
{
    public const int RequestsPerMinute = 100;
    public const int LoginAttemptsPerMinute = 5;
    public const string LoginPolicy = "login";

    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
            {
                return RateLimitPartition.GetFixedWindowLimiter(GetClientAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = RequestsPerMinute,
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

                // Prevent intermediate proxies and edge caches from caching transient 429 rejections.
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Request limit reached.").ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static IPAddress GetClientAddress(HttpContext context)
    {
        // Missing sockets share a conservative partition; mapped IPv6 shares its IPv4 budget.
        var address = context.Connection.RemoteIpAddress ?? IPAddress.None;
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
