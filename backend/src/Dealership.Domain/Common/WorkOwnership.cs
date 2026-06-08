namespace Dealership.Domain.Common;

public static class WorkOwnershipKind
{
    public const string Claimed = "Claimed";

    public const string Assigned = "Assigned";

    public const string Released = "Released";
}

public static class WorkOwnership
{
    public static string? NormalizeReason(string? reason)
    {
        return BoundedText.NormalizeOptional(reason,
            "Ownership reason requires up to 1000 non-control characters.");
    }

    public static bool IsRequestValid(string kind, Guid? requestedHandlerId, string? reason)
    {
        return kind switch
        {
            WorkOwnershipKind.Claimed => requestedHandlerId is null && reason is null,
            WorkOwnershipKind.Assigned => requestedHandlerId is not null,
            WorkOwnershipKind.Released => requestedHandlerId is null,
            _ => false
        };
    }

    public static bool IsTransitionValid(string kind, Guid actorId, Guid? handlerId,
        Guid? previousHandlerId, string? reason)
    {
        return kind switch
        {
            WorkOwnershipKind.Claimed => handlerId == actorId && previousHandlerId is null && reason is null,
            WorkOwnershipKind.Assigned => handlerId is not null && handlerId != previousHandlerId &&
                (previousHandlerId is null || reason is not null),
            WorkOwnershipKind.Released => handlerId is null && previousHandlerId is not null,
            _ => false
        };
    }
}
