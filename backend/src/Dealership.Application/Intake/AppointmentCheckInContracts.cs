using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed record CheckInReceipt(Guid AppointmentId, Guid VehicleId, Guid StaffId,
    DateTimeOffset CheckedInAt, CheckInEvidence Evidence);

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
}
