using Dealership.Application.Accounts;
using static Dealership.Api.Endpoints.HttpContextActor;

namespace Dealership.Api.Endpoints;

public sealed record AssistedOwnerResult(Guid OwnerId, AssistingStaff? Staff, IResult? Error = null);

// A customer's booking belongs to the customer. A staff member who names a customer in this header acts for them
// through the same command, and the services still require that customer to be an active, verified account.
internal static class AssistedOwner
{
    public const string HeaderName = "X-On-Behalf-Of";

    public static async Task<AssistedOwnerResult> ResolveAsync(HttpContext context, IStaffAssistanceQuery assistance,
        CancellationToken cancellationToken)
    {
        var actorId = ActorId(context);
        if (!context.Request.Headers.TryGetValue(HeaderName, out var header))
        {
            return new(actorId, null);
        }

        if (!Guid.TryParse(header.ToString(), out var customerId) || customerId == Guid.Empty)
        {
            return new(actorId, null, Results.Problem(statusCode: 400, title: "Name a valid customer to act for."));
        }

        var staff = await assistance.FindStaffAsync(actorId, cancellationToken);
        return staff is null
            ? new(actorId, null, Results.Forbid())
            : new(customerId, staff);
    }
}
