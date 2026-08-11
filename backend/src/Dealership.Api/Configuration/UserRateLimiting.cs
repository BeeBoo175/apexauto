using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dealership.Api.Configuration;

public sealed class UserRateLimiting : IMiddleware, IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> _reads;
    private readonly PartitionedRateLimiter<HttpContext> _writes;

    public UserRateLimiting(IOptions<RateLimitingSettings> configured)
    {
        var settings = configured.Value;
        _reads = PartitionedRateLimiter.Create<HttpContext, Guid>(context =>
            RateLimitPartition.GetTokenBucketLimiter(Subject(context), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = settings.UserReadBurst,
                TokensPerPeriod = settings.UserReadTokensPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));
        _writes = PartitionedRateLimiter.Create<HttpContext, Guid>(context =>
            RateLimitPartition.GetTokenBucketLimiter(Subject(context), _ =>
                ApiRateLimiting.SmoothBudget(settings.UserWritesPerWindow, TimeSpan.FromSeconds(settings.WindowSeconds))));
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        using var lease = await (read ? _reads : _writes).AcquireAsync(context, cancellationToken: context.RequestAborted);

        if (!lease.IsAcquired)
        {
            await ApiRateLimiting.RejectAsync(context, lease);
            return;
        }

        await next(context);
    }

    public void Dispose()
    {
        _reads.Dispose();
        _writes.Dispose();
    }

    private static Guid Subject(HttpContext context)
    {
        // Authentication has validated the persisted session and nonempty subject before admission.
        return Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    }
}
