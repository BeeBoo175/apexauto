using Dealership.Api.Configuration;
using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

public static class CustomerSupportEndpoints
{
    public static void MapCustomerSupportEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/customer-accounts/{id:guid}").RequireAuthorization().NoStore();
        routes.MapPost("/nric-reveals", RevealAsync);
        routes.MapPost("/session-revocations", (Guid id, CustomerSupportRequest request, HttpContext context,
            CustomerSupportService support, CancellationToken token) => RunAsync(
            support.RevokeSessionsAsync(HttpContextActor.ActorId(context), id, request, token)));
        routes.MapPost("/password-resets", (Guid id, CustomerSupportRequest request, HttpContext context,
            CustomerSupportService support, CancellationToken token) => RunAsync(
            support.SendResetAsync(HttpContextActor.ActorId(context), id, request, token)));
        routes.MapPost("/verification-resends", (Guid id, CustomerSupportRequest request, HttpContext context,
            CustomerSupportService support, CancellationToken token) => RunAsync(
            support.ResendVerificationAsync(HttpContextActor.ActorId(context), id, request, token)));
    }

    private static async Task<IResult> RevealAsync(Guid id, CustomerSupportRequest request, HttpContext context,
        CustomerSupportService support, CancellationToken cancellationToken)
    {
        var result = await support.RevealNricAsync(HttpContextActor.ActorId(context), id, request, cancellationToken);
        return result.Status == CustomerSupportStatus.Succeeded ? Results.Ok(new { nric = result.Nric })
            : Map(result.Status);
    }

    private static async Task<IResult> RunAsync(Task<CustomerSupportStatus> action) => Map(await action);

    private static IResult Map(CustomerSupportStatus status) => status switch
    {
        CustomerSupportStatus.Succeeded => Results.NoContent(),
        CustomerSupportStatus.Forbidden => Results.Forbid(),
        CustomerSupportStatus.NotFound => Results.NotFound(),
        CustomerSupportStatus.Conflict => Results.Conflict(),
        _ => Results.Problem(statusCode: 400, title: "Provide an operation id and a reason of at most 1000 characters.")
    };
}
