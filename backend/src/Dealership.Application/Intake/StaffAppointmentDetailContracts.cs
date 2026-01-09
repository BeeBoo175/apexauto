namespace Dealership.Application.Intake;

public sealed record StaffAppointmentDetail(Guid Id, Guid BranchId, string BranchName, string CustomerName,
    string RegistrationPlate, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Status,
    string Make, string Model, string Trim, int ManufactureYear, int MileageKilometres);

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
}
