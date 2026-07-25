namespace Dealership.Api.Configuration;

public static class EndpointResponseConventions
{
    public static TBuilder NoStore<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
    }
}
