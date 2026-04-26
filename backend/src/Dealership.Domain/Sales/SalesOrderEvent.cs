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
    Cancelled
}

// Append-only record of every deal action, including who acted, when and why.
public sealed class SalesOrderEvent
{
    public const int ReasonMaximumLength = 1000;

    private SalesOrderEvent()
    {
    }

    internal SalesOrderEvent(Guid salesOrderId, SalesOrderEventKind kind, SalesOrderStatus? from, SalesOrderStatus to,
        Guid actorId, string? reason, DateTimeOffset recordedAtUtc)
    {
        Id = Guid.NewGuid();
        SalesOrderId = salesOrderId;
        Kind = kind;
        FromStatus = from;
        ToStatus = to;
        ActorId = actorId;
        Reason = reason;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SalesOrderId { get; private set; }

    public SalesOrderEventKind Kind { get; private set; }

    public SalesOrderStatus? FromStatus { get; private set; }

    public SalesOrderStatus ToStatus { get; private set; }

    public Guid ActorId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}
