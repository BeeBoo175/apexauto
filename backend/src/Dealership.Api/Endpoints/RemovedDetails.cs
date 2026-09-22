namespace Dealership.Api.Endpoints;

// The answer a sale command or document read gives once the retention period removed the buyer's personal details
// from the sale's records: the commercial record remains, but nothing can be issued or corrected from the details.
internal static class RemovedDetails
{
    public const string Code = "details-removed";

    public static IResult Problem()
    {
        return CodedProblem.Conflict(Code, "The buyer's personal details were removed after the retention period.");
    }
}