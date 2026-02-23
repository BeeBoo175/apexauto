using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed record CancellationReceipt(Guid AppointmentId, DateTimeOffset CancelledAt);

public enum CancellationStatus
{
    Succeeded,
    Forbidden,
    NotFound,
    TooLate,
    CheckedIn,
    Invalid
}

public sealed record CancellationResult(CancellationStatus Status, CancellationReceipt? Receipt = null);

public sealed record CancellationCustomer(bool IsActive, bool EmailConfirmed, string Role, bool HasProfile);

public sealed record CancellationContext(CancellationCustomer? Customer, IntakeAppointment? Appointment,
    bool CheckedIn, TimeSpan PhotoRetention);

public interface IAppointmentCancellationTransaction : IAsyncDisposable
{
    CancellationContext Context { get; }

    Task<CancellationStatus> CommitAsync(IntakeAppointment appointment, CancellationToken cancellationToken);
}

public interface IAppointmentCancellationStore
{
    Task<IAppointmentCancellationTransaction> LockAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken);
}
