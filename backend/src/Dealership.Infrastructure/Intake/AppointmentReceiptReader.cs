using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

internal static class AppointmentReceiptReader
{
    public static async Task<AppointmentReceipt?> ReadAsync(DealershipDbContext database, Guid id,
        Guid ownerId, CancellationToken cancellationToken)
    {
        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        var valuation = await database.SellerValuations.AsNoTracking()
            .SingleAsync(item => item.AppointmentId == id, cancellationToken);
        var photos = await database.AppointmentPhotos.AsNoTracking().Where(item => item.AppointmentId == id)
            .Select(item => new BookingPhotoSelection(item.PhotoId, item.Angle))
            .ToArrayAsync(cancellationToken);
        return new AppointmentReceipt(id, appointment.BranchId, appointment.RegistrationPlate,
            new InspectionSlot(appointment.StartsAt, appointment.EndsAt), appointment.CreatedAt,
            new ValuationRequest(valuation.Make, valuation.Model, valuation.Trim, valuation.ManufactureYear, valuation.MileageKilometres),
            new ValuationResponse(valuation.Minimum, valuation.Maximum, valuation.Currency, valuation.ValuationYear),
            photos.OrderBy(photo => photo.Angle).ToArray());
    }
}
