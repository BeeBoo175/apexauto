using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class PersonEndpoints
{
    public static void MapPersonEndpoints(this WebApplication app)
    {
        app.MapGet("/api/people/{id:guid}", GetPersonAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetPersonAsync(Guid id, HttpContext context, PersonViewService people,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var view = await people.GetAsync(HttpContextActor.ActorId(context), id, cancellationToken);
        return view.Status switch
        {
            PersonViewStatus.Found => Results.Ok(view.Person),
            PersonViewStatus.NotFound => Results.NotFound(),
            _ => Results.Forbid()
        };
    }
}
