using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", RegisterAsync).NoStore();
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, RegistrationService service,
        CancellationToken cancellationToken)
    {
        var (request, error) = await JsonRequestReader.ReadAsync<RegistrationRequest>(context.Request, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var result = await service.RegisterAsync(request!, cancellationToken);

        return result.Status switch
        {
            RegistrationStatus.Succeeded => Results.NoContent(),
            RegistrationStatus.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Registration conflicts with an existing account or request. Use existing-account recovery."),
            _ => Results.ValidationProblem(result.Errors.ToDictionary())
        };
    }
}
