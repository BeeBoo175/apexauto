namespace Dealership.Api.Endpoints;

// A payment provider's server callback: a signed form, no session. The service decides whether the signature is
// valid; this reads the form once for every provider route.
internal static class ProviderCallback
{
    public static async Task<IResult> ReadFormAsync(HttpContext context,
        Func<IReadOnlyDictionary<string, string>, CancellationToken, Task<bool>> complete,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var fields = form.ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal);
        return await complete(fields, cancellationToken)
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status400BadRequest);
    }
}