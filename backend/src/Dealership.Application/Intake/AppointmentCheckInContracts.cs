using Dealership.Domain.Intake;

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

public interface IAppointmentCheckInStore
{
    Task<CheckInResult> CheckInAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        CancellationToken cancellationToken);

    Task<CheckInResult> CorrectNoShowAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
        NoShowCorrectionRequest correction, CancellationToken cancellationToken);
}
