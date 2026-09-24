using Dealership.Api.Configuration;
using Dealership.Application.Accounts;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class RetentionSettingsEndpoints
{
    public static void MapRetentionSettingsEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/settings/retention").RequireAuthorization().NoStore();
        routes.MapGet("", ReadAsync);
        routes.MapPut("", UpdateAsync);
    }

    private static async Task<IResult> ReadAsync(HttpContext context, RetentionSettingsService settings,
        CancellationToken cancellationToken)
    {
        return await BoundedRead.RunAsync(async token => Map(await settings.ReadAsync(ActorId(context), token)),
            "Retention settings are temporarily unavailable.", cancellationToken);
    }

    private static async Task<IResult> UpdateAsync(HttpContext context, RetentionSettingsService settings,
        CancellationToken cancellationToken)
    {
        return await BoundedCommand.RunAsync(async deadline =>
        {
            var (request, error) = await JsonRequestReader.ReadAsync<RetentionSettingsUpdateRequest>(context.Request,
                deadline);
            if (error is not null)
            {
                return error;
            }

            return Map(await settings.UpdateAsync(ActorId(context), request!, deadline));
        }, () => Results.Problem(statusCode: 503,
            title: "The retention change is unconfirmed; reload the settings before retrying."), cancellationToken);
    }

    private static IResult Map(RetentionSettingsResult result) => result.Status switch
    {
        RetentionSettingsStatus.Succeeded => Results.Ok(result.Settings),
        RetentionSettingsStatus.Invalid => Results.Problem(statusCode: 400,
            title: "Provide retention durations within the approved bounds."),
        RetentionSettingsStatus.Forbidden => Results.Forbid(),
        RetentionSettingsStatus.NotFound => Results.NotFound(),
        RetentionSettingsStatus.Conflict => Results.Json(new { code = "settings-changed" }, statusCode: 409),
        RetentionSettingsStatus.ConfirmationRequired => Results.Json(new { code = "shortening-confirmation-required" },
            statusCode: 409),
        _ => Results.Problem(statusCode: 500)
    };
}
