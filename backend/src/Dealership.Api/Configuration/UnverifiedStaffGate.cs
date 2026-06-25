using Dealership.Api.Endpoints;
using Dealership.Application.Accounts;

namespace Dealership.Api.Configuration;

// A staff account with an unconfirmed email may sign in, read its own account, set its picture and verify, and
// nothing else. The gate keys on the signed-in account, so every present and future protected endpoint is covered.
public static class UnverifiedStaffGate
{
    private static readonly string[] AllowedPrefixes =
    [
        "/api/auth/",
        "/api/account/profile",
        "/api/accounts/"
    ];

    public static IApplicationBuilder UseUnverifiedStaffGate(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true && !IsAllowed(context.Request.Path))
            {
                var sessions = context.RequestServices.GetRequiredService<IAccountSessionStore>();

                if (await sessions.IsUnverifiedStaffAsync(HttpContextActor.ActorId(context), context.RequestAborted))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await next();
        });
    }

    private static bool IsAllowed(PathString path)
    {
        return AllowedPrefixes.Any(prefix => path.StartsWithSegments(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }
}
