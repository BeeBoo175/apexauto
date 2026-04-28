namespace Dealership.Domain.Sales;

public enum SalesOrderStatus
{
    Draft,
    PendingManagerApproval,
    Approved,
    Cancelled
}

public enum SalesOrderEventKind
{
    Created,
    Revised,
    Submitted,
    Approved,
    Rejected,
    Cancelled,
    // The deal moved to another agent; its status is unchanged.
    Reassigned
}

// One term that a revision changed, with the values either side as plain invariant text.
public sealed record DealChange(string Field, string? Before, string? After)
{
    public const string NegotiatedDiscount = nameof(NegotiatedDiscount);
    public const string Warranty = nameof(Warranty);
    public const string TradeInAllowance = nameof(TradeInAllowance);
}

// Append-only record of every deal action, including who acted, when and why.
public sealed class SalesOrderEvent
{
    public const int ReasonMaximumLength = 1000;

    private SalesOrderEvent()
    {
    }

    internal SalesOrderEvent(Guid salesOrderId, long sequence, SalesOrderEventKind kind, SalesOrderStatus? from,
        SalesOrderStatus to, Guid actorId, string? reason, DateTimeOffset recordedAtUtc,
        IReadOnlyList<DealChange>? changes = null)
    {
        Id = Guid.NewGuid();
        SalesOrderId = salesOrderId;
        Sequence = sequence;
        Kind = kind;
        FromStatus = from;
        ToStatus = to;
        ActorId = actorId;
        Reason = reason;
        RecordedAtUtc = recordedAtUtc;
        Changes = changes is { Count: > 0 } ? changes : null;
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    // The deal's version after this action, so history order never depends on timestamps.
    public long Sequence { get; private set; }

    public SalesOrderEventKind Kind { get; private set; }

    public SalesOrderStatus? FromStatus { get; private set; }

    public SalesOrderStatus ToStatus { get; private set; }

    public Guid ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    // What a revision changed, so history can say "from X to Y" instead of only "terms changed".
    public IReadOnlyList<DealChange>? Changes { get; private set; }
}
