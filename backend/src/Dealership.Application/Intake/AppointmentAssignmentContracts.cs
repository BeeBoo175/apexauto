using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed record AppointmentAssignmentRequest(Guid OperationId, long ExpectedVersion,
    Guid? HandlerId = null, string? Reason = null);

public enum AppointmentAssignmentStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable,
    ScheduleConflict
}

public sealed record AppointmentAssignmentResult(AppointmentAssignmentStatus Status,
    AppointmentAssignment? Receipt = null, AppointmentAssignment? Current = null);

public sealed record AppointmentAssignmentContext(IntakeAppointment? Appointment, CheckInStaff? Actor,
    CheckInStaff? Handler, bool BranchActive, bool CheckedIn, AppointmentAssignment? Existing,
    AppointmentAssignment? Current);

public interface IAppointmentAssignmentStore
{
    Task<IAppointmentAssignmentTransaction> LockAsync(Guid actorId, Guid appointmentId,
        AppointmentAssignmentRequest request, CancellationToken cancellationToken);
}

public interface IAppointmentAssignmentTransaction : IAsyncDisposable
{
    AppointmentAssignmentContext Context { get; }

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken);

    Task CommitAsync(AppointmentAssignment assignment, CancellationToken cancellationToken);
}
