namespace Dealership.Application.Intake;

public sealed record CancellationReceipt(Guid AppointmentId, DateTimeOffset CancelledAt);

public enum CancellationStatus
{
    Succeeded,
    Forbidden,
    NotFound,
    TooLate,
    Invalid
}

public sealed record CancellationResult(CancellationStatus Status, CancellationReceipt? Receipt = null);

public interface IAppointmentCancellationStore
{
    Task<CancellationResult> CancelAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken);
}
