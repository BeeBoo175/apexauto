namespace Dealership.Api.Endpoints;

// The one answer a staff command gives when the customer it targets has closed their account: nothing about the
// customer is revealed beyond the state, and the code is stable for the browser to recognise.
internal static class ClosedCustomer
{
    public const string Code = "customer-closed";

    public static IResult Problem()
    {
        return Results.Problem(statusCode: 409, title: "This customer has closed their account.",
            extensions: new Dictionary<string, object?> { ["code"] = Code });
    }
}
