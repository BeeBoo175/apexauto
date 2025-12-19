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
        app.MapPost("/api/auth/email-verification/resend", ResendVerificationAsync)
            .RequireAuthorization().RequireRateLimiting(ApiRateLimiting.VerificationPolicy);
        app.MapPost("/api/auth/email-verification", ConfirmEmailAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy);
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

    private static async Task<IResult> ResendVerificationAsync(HttpContext context, EmailVerificationService verification,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var (request, error) = await JsonRequestReader.ReadAsync<ResendVerificationRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var userId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var result = await verification.ResendAsync(userId, request!.ResendKey, cancellationToken);

        if (result.Status == VerificationStatus.RateLimited)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(result.RetryAfter!.Value.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                title: "Wait before requesting another verification email.");
        }

        return result.Status switch
        {
            VerificationStatus.Forbidden => Results.Forbid(),
            VerificationStatus.Invalid => Results.Problem(statusCode: StatusCodes.Status400BadRequest),
            _ => Results.Accepted()
        };
    }

    private static async Task<IResult> ConfirmEmailAsync(HttpContext context, EmailVerificationService verification,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var (request, error) = await JsonRequestReader.ReadAsync<EmailVerificationRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var result = await verification.ConfirmAsync(request!.Token, cancellationToken);
        return result.Status == VerificationStatus.Accepted
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verification link is invalid or expired.");
    }

    private sealed record EmailVerificationRequest(string? Token);

    private sealed record ResendVerificationRequest(Guid ResendKey);
}
