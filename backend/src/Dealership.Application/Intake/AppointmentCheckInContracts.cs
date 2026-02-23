using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Intake;

public sealed record CheckInReceipt(Guid AppointmentId, Guid VehicleId, Guid StaffId,
    DateTimeOffset CheckedInAt, CheckInEvidence Evidence, CorrectionReceipt? Correction = null);

public sealed record CorrectionReceipt(DateTimeOffset RecordedAt, string Reason);

public sealed record NoShowCorrectionRequest(DateTimeOffset AttendedAt, string Reason);

public enum CheckInStatus
{
    Succeeded,
    Forbidden,
    NotFound,
    Invalid,
    Conflict,
    Unavailable,
    VehicleConflict
}

public sealed record CheckInResult(CheckInStatus Status, CheckInReceipt? Receipt = null);

public sealed record CheckInStaff(bool IsActive, string Role, Guid? BranchId);

public sealed record CheckInCustomer(bool IsActive, bool EmailConfirmed, string Role);

public sealed record CheckInContext(IntakeAppointment? Appointment, CheckInStaff? Staff,
    CheckInCustomer? Customer, bool BranchActive, AppointmentCheckIn? Existing);

public sealed record CheckInPhotoCandidate(Guid Id, DateTimeOffset? AvailableAt, bool RemovalPending);

public interface IAppointmentCheckInTransaction : IAsyncDisposable
{
    CheckInContext Context { get; }

    Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CheckInPhotoCandidate>> LockPhotosAsync(CancellationToken cancellationToken);

    Task<CheckInStatus> CommitAsync(Vehicle vehicle, AppointmentCheckIn checkIn, DateTimeOffset recordedAt,
        CancellationToken cancellationToken);
}

public interface IAppointmentCheckInStore
{
    Task<IAppointmentCheckInTransaction> LockAsync(Guid staffId, Guid appointmentId, CancellationToken cancellationToken);
}
