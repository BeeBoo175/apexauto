namespace Dealership.Domain.Vehicles;

public enum ReservationEventKind
{
    Claimed,
    Assigned,
    Reassigned,
    Released
}

// Append-only record of who took, was given or gave back a hold, so a claim's history never depends on its current state.
public sealed class ReservationEvent
{
    private ReservationEvent()
    {
    }

    internal ReservationEvent(Guid reservationId, int sequence, ReservationEventKind kind, Guid actorId, Guid? agentId,
        Guid? previousAgentId, DateTimeOffset recordedAtUtc)
    {
        Id = Guid.NewGuid();
        ReservationId = reservationId;
        Sequence = sequence;
        Kind = kind;
        ActorId = actorId;
        AgentId = agentId;
        PreviousAgentId = previousAgentId;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    // One-based position in the reservation's own history, so order never depends on timestamps.
    public int Sequence { get; private set; }

    public ReservationEventKind Kind { get; private set; }

    public Guid ActorId { get; private set; }

    // The agent handling the hold after this event; empty once it is released.
    public Guid? AgentId { get; private set; }

    // The agent who handled it before this event, when there was one.
    public Guid? PreviousAgentId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}
