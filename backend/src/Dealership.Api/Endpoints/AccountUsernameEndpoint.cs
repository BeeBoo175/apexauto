using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class AccountUsernameEndpoint
{
    public static void MapAccountUsernameEndpoint(this WebApplication app)
    {
        app.MapPut("/api/account/username", ChangeAsync).RequireAuthorization();
    }

    private static async Task<IResult> ChangeAsync(AccountUsernameRequest request, HttpContext context,
        AccountUsernameService usernames, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var status = await usernames.ChangeAsync(HttpContextActor.ActorId(context), request, cancellationToken);
        return status switch
        {
            AccountUsernameStatus.Changed => Results.NoContent(),
            AccountUsernameStatus.Taken => Results.Conflict(new { message = "That username is taken. Choose another." }),
            AccountUsernameStatus.Forbidden => Results.Forbid(),
            _ => Results.BadRequest(new { message = "Usernames use 3 to 30 letters, numbers, dots or underscores." })
        };
    }
}
