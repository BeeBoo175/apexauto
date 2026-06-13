using System.IdentityModel.Tokens.Jwt;

namespace Dealership.Api.Endpoints;

internal static class HttpContextActor
{
    public static Guid ActorId(HttpContext context) =>
        Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
