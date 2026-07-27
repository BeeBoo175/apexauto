using System.IdentityModel.Tokens.Jwt;
using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", LoginAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy).NoStore();
        app.MapPost("/api/auth/logout", LogoutAsync).RequireAuthorization().NoStore();
        app.MapGet("/api/account", GetAccountAsync).RequireAuthorization().NoStore();
        app.MapGet("/api/account/profile", GetProfileAsync).RequireAuthorization().NoStore();
        app.MapGet("/api/auth/session", GetActorAsync).RequireAuthorization().NoStore();
        app.MapPost("/api/auth/email-verification/resend", ResendVerificationAsync)
            .RequireAuthorization().RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
        app.MapPost("/api/auth/email-verification", ConfirmEmailAsync)
            .RequireRateLimiting(ApiRateLimiting.VerificationPolicy).NoStore();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
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
        var userId = HttpContextActor.ActorId(context);
        var sessionId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value);
        await sessions.RevokeAsync(userId, sessionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAccountAsync(HttpContext context, ICustomerAccountQuery accounts,
        CancellationToken cancellationToken)
    {
        var userId = HttpContextActor.ActorId(context);
        return await BoundedRead.RunAsync(async token =>
        {
            var account = await accounts.GetAccountAsync(userId, token);
            return account is null ? Results.Forbid() : Results.Ok(account);
        }, "Account details are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> GetProfileAsync(HttpContext context, IAccountProfileQuery profiles,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token =>
        {
            var profile = await profiles.GetProfileAsync(HttpContextActor.ActorId(context), token);
            return profile is null ? Results.Forbid() : Results.Ok(profile);
        }, "Account profile is temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> GetActorAsync(HttpContext context, IAccountSessionStore sessions,
        CancellationToken cancellationToken)
    {
        var userId = HttpContextActor.ActorId(context);
        var sessionId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value);
        return await BoundedRead.RunAsync(async token =>
        {
            var actor = await sessions.GetActorAsync(userId, sessionId, token);
            return actor is null ? Results.Unauthorized() : Results.Ok(actor);
        }, "Session details are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> ResendVerificationAsync(HttpContext context, EmailVerificationService verification,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<ResendVerificationRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var userId = HttpContextActor.ActorId(context);
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
