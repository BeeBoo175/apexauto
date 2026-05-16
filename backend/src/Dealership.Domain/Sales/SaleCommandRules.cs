namespace Dealership.Domain.Sales;

internal static class SaleCommandRules
{
    public static void RequireCommand(Guid operationId, Guid actorId, DateTimeOffset now, DateTimeOffset updatedAt)
    {
        if (operationId == Guid.Empty || actorId == Guid.Empty || now.Offset != TimeSpan.Zero || now < updatedAt)
        {
            throw new ArgumentException("A sale action needs an operation, an actor and a UTC time that does not go back.");
        }
    }

    public static void RequireAgent(Guid agentId)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("A sale is handled by a named agent.");
        }
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static string RequiredReason(string reason) => SalesOrder.ValidReason(reason)
        ? reason
        : throw new ArgumentException("A reason of 1 to 1000 trimmed characters is required.");
}
