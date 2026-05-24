namespace Dealership.Domain.Intake;

public static class AppointmentAssignmentKind
{
    public const string Claimed = "Claimed";
    public const string Assigned = "Assigned";
    public const string Released = "Released";
}

public sealed class AppointmentAssignment
{
    private AppointmentAssignment()
    {
    }

    public AppointmentAssignment(Guid id, IntakeAppointment appointment, Guid actorId, string kind,
        Guid? handlerId, AppointmentAssignment? previous, string? reason, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        reason = NormalizeReason(reason);
        if (id == Guid.Empty || actorId == Guid.Empty || handlerId == Guid.Empty ||
            previous is not null && previous.AppointmentId != appointment.Id ||
            previous?.Version == long.MaxValue || recordedAt < appointment.CreatedAt ||
            previous is not null && recordedAt < previous.RecordedAt)
        {
            throw new ArgumentException("Assignment requires valid identities and consecutive history.");
        }

        var previousHandler = previous?.HandlerId;
        var valid = kind switch
        {
            AppointmentAssignmentKind.Claimed => handlerId == actorId && previousHandler is null && reason is null,
            AppointmentAssignmentKind.Assigned => handlerId is not null && handlerId != previousHandler &&
                (previousHandler is null || reason is not null),
            AppointmentAssignmentKind.Released => handlerId is null && previousHandler is not null,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException("Assignment does not match its ownership transition.");
        }

        Id = id;
        AppointmentId = appointment.Id;
        BranchId = appointment.BranchId;
        ActorId = actorId;
        Kind = kind;
        HandlerId = handlerId;
        PreviousHandlerId = previousHandler;
        ExpectedVersion = previous?.Version ?? 0;
        Version = ExpectedVersion + 1;
        Reason = reason;
        RecordedAt = recordedAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public Guid? HandlerId { get; private set; }
    public Guid? PreviousHandlerId { get; private set; }
    public long ExpectedVersion { get; private set; }
    public long Version { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static string? NormalizeReason(string? reason)
    {
        if (reason is null)
        {
            return null;
        }

        reason = reason.Trim();
        if (reason.Length is < 1 or > 1000 || reason.Any(char.IsControl))
        {
            throw new ArgumentException("Ownership reason requires up to 1000 non-control characters.");
        }

        return reason;
    }
}
