namespace Dealership.Domain.Sales;

public enum SalesOrderStatus
{
    Open,
    Handling,
    PendingApproval,
    Approved,
    Expired,
    Cancelled
}

public enum SalesOrderEventKind
{
    Created,
    Claimed,
    Assigned,
    Reassigned,
    Released,
    TermsSaved,
    Submitted,
    Approved,
    Rejected,
    Cancelled,
    CustomerCancelled,
    // Recorded by the database when an open sale passes its deadline.
    Expired,
    // Recorded by the database when the listing is withdrawn or the vehicle goes back for inspection.
    Withdrawn
}

// One term that an action changed, with the values either side as plain invariant text.
public sealed record DealChange(string Field, string? Before, string? After)
{
    public const string NegotiatedDiscount = nameof(NegotiatedDiscount);
    public const string Warranty = nameof(Warranty);
    public const string TradeIn = nameof(TradeIn);
    public const string TradeInAllowance = nameof(TradeInAllowance);
}

// The sale's only history: one append-only receipt per action, keyed by the action's operation identity and
// numbered by the sale's version, each keeping the whole sale as it stood afterwards.
public sealed class SalesOrderEvent
{
    public const int ReasonMaximumLength = 1000;

    private SalesOrderEvent()
    {
    }

    internal SalesOrderEvent(Guid operationId, SalesOrder order, SalesOrderEventKind kind, SalesOrderStatus? from,
        Guid? actorId, Guid? previousAgentId, string? reason, IReadOnlyList<DealChange>? changes,
        DateTimeOffset recordedAt)
    {
        Id = operationId;
        SalesOrderId = order.Id;
        Sequence = order.Version;
        Kind = kind;
        FromStatus = from;
        ToStatus = order.Status;
        ActorId = actorId;
        AgentId = order.AgentId;
        PreviousAgentId = previousAgentId;
        Reason = reason;
        Changes = changes is { Count: > 0 } ? changes : null;
        Snapshot = DealSnapshot.Of(order);
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    // Equals the sale's version after this action, so history order never depends on timestamps.
    public long Sequence { get; private set; }

    public SalesOrderEventKind Kind { get; private set; }

    public SalesOrderStatus? FromStatus { get; private set; }

    public SalesOrderStatus ToStatus { get; private set; }

    // Empty only for actions the database records on its own, such as expiry and withdrawal.
    public Guid? ActorId { get; private set; }

    // The agent handling the sale after this action.
    public Guid? AgentId { get; private set; }

    // The agent who handled it before, when this action moved or released it.
    public Guid? PreviousAgentId { get; private set; }

    public string? Reason { get; private set; }

    public IReadOnlyList<DealChange>? Changes { get; private set; }

    public DealSnapshot Snapshot { get; private set; } = null!;

    // The refund receipt released by an expiry or cancellation.
    public Guid? ReleaseId { get; init; }

    public DateTimeOffset RecordedAt { get; private set; }
}
