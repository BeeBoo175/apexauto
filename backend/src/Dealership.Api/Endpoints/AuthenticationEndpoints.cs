using System.IdentityModel.Tokens.Jwt;
using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", LoginAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy);
        app.MapPost("/api/auth/logout", LogoutAsync).RequireAuthorization();
        app.MapGet("/api/account", GetAccountAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var (request, error) = await JsonRequestReader.ReadAsync<LoginRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var session = await authentication.LoginAsync(request!, cancellationToken);
        return session is null
            ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email or password is incorrect.")
            : Results.Ok(session);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAccountSessionStore sessions,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var sessionId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value);
        await sessions.RevokeAsync(userId, sessionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAccountAsync(HttpContext context, IAccountSessionStore sessions,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var account = await sessions.GetAccountAsync(userId, cancellationToken);
        return account is null ? Results.Forbid() : Results.Ok(account);
    }
}
