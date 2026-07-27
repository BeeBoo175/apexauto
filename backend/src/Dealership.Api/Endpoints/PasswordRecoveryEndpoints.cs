using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class PasswordRecoveryEndpoints
{
    public static void MapPasswordRecoveryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/password-recovery", RequestAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy).NoStore();
        app.MapPost("/api/auth/password-reset", ResetAsync).RequireRateLimiting(ApiRateLimiting.LoginPolicy).NoStore();
    }

    private static async Task<IResult> RequestAsync(HttpContext context, PasswordRecoveryService recovery,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<PasswordRecoveryRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        await recovery.RequestAsync(request!, cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ResetAsync(HttpContext context, PasswordRecoveryService recovery,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<PasswordResetRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var result = await recovery.ResetAsync(request!, cancellationToken);
        return result.Status switch
        {
            PasswordResetStatus.Succeeded => Results.NoContent(),
            PasswordResetStatus.PasswordRejected => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["password"] = result.PasswordErrors! }),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Reset link is invalid or expired.")
        };
    }
}
