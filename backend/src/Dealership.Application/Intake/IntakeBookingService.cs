using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;

namespace Dealership.Application.Intake;

public sealed class IntakeBookingService(IIntakeBookingStore bookings, ValuationService valuations,
    InspectionSchedule schedule, TimeProvider clock)
{
    public async Task<BookingResult> BookAsync(Guid ownerId, BookingRequest request, CancellationToken cancellationToken)
    {
        if (!HasValidShape(request))
        {
            return new BookingResult(BookingStatus.Invalid);
        }

        var normalized = request with
        {
            RegistrationPlate = request.RegistrationPlate!.Trim().ToUpperInvariant(),
            StartsAt = request.StartsAt.ToUniversalTime()
        };
        await using var transaction = await bookings.LockAsync(ownerId, normalized, cancellationToken);
        var context = transaction.Context;

        if (context.Status != BookingStatus.Ready)
        {
            return new BookingResult(context.Status);
        }

        if (context.Existing is not null)
        {
            return Matches(context.Existing, normalized)
                ? new BookingResult(BookingStatus.Succeeded, context.Existing)
                : new BookingResult(BookingStatus.Conflict);
        }

        var now = clock.GetUtcNow();
        var slot = schedule.FindSlot(normalized.StartsAt, now);

        if (!context.BranchAvailable || slot is null)
        {
            return new BookingResult(BookingStatus.SlotUnavailable);
        }

        var calculation = valuations.Calculate(normalized.Vehicle!);

        if (calculation.Quote is null)
        {
            return new BookingResult(BookingStatus.Invalid);
        }

        if (!SameAmount(calculation.Quote, normalized.AcceptedQuote!))
        {
            return new BookingResult(BookingStatus.QuoteChanged, FreshQuote: calculation.Quote);
        }

        if (!BookingPhotoSet.TryCreate(ownerId, context.Photos, now, out var photoSet))
        {
            return new BookingResult(BookingStatus.PhotosUnavailable);
        }

        var vehicle = normalized.Vehicle!;
        var quote = calculation.Quote;
        var valuation = new SellerValuation(vehicle.Make!, vehicle.Model!,
            vehicle.Trim!, vehicle.ManufactureYear!.Value, vehicle.MileageKilometres!.Value,
            quote.MinValuation, quote.MaxValuation, quote.Currency, quote.ValuationYear);
        var appointment = new IntakeAppointment(normalized.Id, ownerId, normalized.BranchId,
            normalized.RegistrationPlate!, slot, now, valuation);
        var attachments = photoSet!.Selections.Select(photo => new AppointmentPhoto(appointment.Id, ownerId, photo)).ToArray();
        return await transaction.CommitAsync(appointment, attachments, cancellationToken);
    }

    public async Task<BookingResult> GetReceiptAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        return await bookings.FindAsync(ownerId, id, cancellationToken);
    }

    private static bool HasValidShape(BookingRequest request)
    {
        return request.Id != Guid.Empty && request.BranchId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(request.RegistrationPlate) && request.RegistrationPlate.Trim().Length <= 20 &&
            request.Vehicle is not null && request.AcceptedQuote is not null &&
            request.PhotoIds is { Count: 4 } && request.PhotoIds.All(id => id != Guid.Empty) &&
            request.PhotoIds.Distinct().Count() == 4;
    }

    private static bool SameAmount(ValuationResponse first, ValuationResponse second)
    {
        return first.MinValuation == second.MinValuation && first.MaxValuation == second.MaxValuation &&
            first.Currency == second.Currency;
    }

    private static bool Matches(AppointmentReceipt receipt, BookingRequest request)
    {
        return receipt.BranchId == request.BranchId && receipt.RegistrationPlate == request.RegistrationPlate &&
            receipt.Slot.StartsAt == request.StartsAt && receipt.Vehicle == request.Vehicle &&
            SameAmount(receipt.Quote, request.AcceptedQuote!) &&
            receipt.Photos.Select(photo => photo.Id).Order().SequenceEqual(request.PhotoIds!.Order());
    }
}
