using Dealership.Application.Vehicles;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/staff/settings").RequireAuthorization();
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        routes.MapGet("", ReadAsync);
        routes.MapPut("", UpdateAsync);
    }

    private static async Task<IResult> ReadAsync(HttpContext context, PurchaseSettingsAdministrationService settings,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await settings.ReadAsync(ActorId(context), deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static async Task<IResult> UpdateAsync(HttpContext context, PurchaseSettingsAdministrationService settings,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var (request, error) = await JsonRequestReader.ReadAsync<PurchaseSettingsUpdateRequest>(context.Request, deadline.Token);
            if (error is not null)
            {
                return error;
            }

            var result = await settings.UpdateAsync(ActorId(context), request!, deadline.Token);
            return Map(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503,
                title: "The settings change is unconfirmed; reload the settings before retrying.");
        }
    }

    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Settings are temporarily unavailable.");

    private static IResult Map(PurchaseSettingsAdministrationResult result) => result.Status switch
    {
        PurchaseSettingsAdministrationStatus.Succeeded => Results.Ok(result.Settings),
        PurchaseSettingsAdministrationStatus.Invalid => Results.Problem(statusCode: 400, title: "Provide settings within the approved bounds."),
        PurchaseSettingsAdministrationStatus.Forbidden => Results.Forbid(),
        PurchaseSettingsAdministrationStatus.NotFound => Results.NotFound(),
        PurchaseSettingsAdministrationStatus.Conflict => Results.Problem(statusCode: 409,
            title: "The settings changed since they were loaded; reload and retry."),
        _ => Results.Problem(statusCode: 500)
    };
}
