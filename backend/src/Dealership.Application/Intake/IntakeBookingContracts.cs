using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed record BookingRequest(Guid Id, Guid BranchId, DateTimeOffset StartsAt, string? RegistrationPlate,
    ValuationRequest? Vehicle, ValuationResponse? AcceptedQuote, IReadOnlyList<Guid>? PhotoIds);

public sealed record AppointmentReceipt(Guid Id, Guid BranchId, string RegistrationPlate, InspectionSlot Slot,
    DateTimeOffset CreatedAt, ValuationRequest Vehicle, ValuationResponse Quote, IReadOnlyList<BookingPhotoSelection> Photos);

public enum BookingStatus
{
    Ready,
    Succeeded,
    Invalid,
    Forbidden,
    Conflict,
    QuoteChanged,
    SlotUnavailable,
    PhotosUnavailable,
    NotFound
}

public sealed record BookingResult(BookingStatus Status, AppointmentReceipt? Receipt = null, ValuationResponse? FreshQuote = null);

public sealed record BookingContext(BookingStatus Status, AppointmentReceipt? Existing = null,
    bool BranchAvailable = false, IReadOnlyList<BookingPhotoCandidate>? Photos = null);

public interface IIntakeBookingTransaction : IAsyncDisposable
{
    BookingContext Context { get; }

    Task<BookingResult> CommitAsync(IntakeAppointment appointment,
        IReadOnlyList<AppointmentPhoto> photos, CancellationToken cancellationToken);
}

public interface IIntakeBookingStore
{
    Task<IIntakeBookingTransaction> LockAsync(Guid ownerId, BookingRequest request, CancellationToken cancellationToken);

    Task<BookingResult> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InspectionSlot>> GetOccupiedSlotsAsync(Guid branchId, DateTimeOffset first,
        DateTimeOffset last, CancellationToken cancellationToken);
}
