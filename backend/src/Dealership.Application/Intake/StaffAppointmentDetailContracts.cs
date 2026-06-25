using Dealership.Application.Accounts;

namespace Dealership.Application.Intake;

public sealed record StaffAppointmentDetail(Guid Id, Guid BranchId, string BranchName, string CustomerName,
    string RegistrationPlate, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status,
    string Make, string Model, string Trim, int ManufactureYear, int MileageKilometres, DateTimeOffset? NoShowAt = null,
    DateTimeOffset? AttendanceCorrectedAt = null, string? CorrectionReason = null, Guid? CorrectedBy = null,
    string CustomerMobile = "", string CustomerEmail = "", string? CorrectedByEmail = null,
    AppointmentOwnership? Ownership = null);

public sealed record AppointmentOwnership(long Version, Guid? HandlerId, string? HandlerEmail,
    bool HandlerEligible, Guid ActorId, string Kind, DateTimeOffset RecordedAt, string? Reason,
    string? HandlerName = null, Guid? HandlerPictureId = null);

public sealed record AppointmentHandlersResult(StaffAppointmentDetailStatus Status,
    IReadOnlyList<StaffHandler>? Handlers = null);

public enum StaffAppointmentDetailStatus
{
    Succeeded,
    Forbidden,
    NotFound
}

public sealed record StaffAppointmentDetailResult(StaffAppointmentDetailStatus Status, StaffAppointmentDetail? Detail = null);

public interface IStaffAppointmentDetailStore
{
    Task<StaffAppointmentDetailResult> GetAsync(Guid staffId, Guid appointmentId, CancellationToken cancellationToken);

    Task<AppointmentHandlersResult> HandlersAsync(Guid staffId, Guid appointmentId, CancellationToken cancellationToken);
}
