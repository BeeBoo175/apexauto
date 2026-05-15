using Dealership.Application.Sales;
using Dealership.Application.Vehicles;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleVehiclePhotoStore(DealershipDbContext database) : ISaleVehiclePhotoStore
{
    public async Task<ListingPhotoPreparation> FindAsync(Guid actorId, Guid saleId, Guid id,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status == SaleResultStatus.Forbidden ? ListingPhotoStatus.Forbidden : ListingPhotoStatus.NotFound);
        }

        // The retained reserved publication grants access, never the current draft or private evidence.
        var vehicleId = await (from reservation in database.Reservations
                               join publication in database.ListingPublications on reservation.PublicationId equals publication.Id
                               join photo in database.ListingRevisionPhotos on publication.RevisionId equals photo.RevisionId
                               where reservation.Id == saleId && photo.PhotoId == id
                               select (Guid?)reservation.VehicleId).SingleOrDefaultAsync(cancellationToken);
        if (!vehicleId.HasValue)
        {
            return new(ListingPhotoStatus.NotFound);
        }

        var stored = await database.ListingPhotos.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == id && item.VehicleId == vehicleId.Value, cancellationToken);
        return stored is null
            ? new(ListingPhotoStatus.NotFound)
            : new(stored.AvailableAt is null ? ListingPhotoStatus.Pending : ListingPhotoStatus.Available, stored);
    }
}
