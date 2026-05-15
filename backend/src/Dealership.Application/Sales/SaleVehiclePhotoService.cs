using Dealership.Application.Photos;
using Dealership.Application.Vehicles;

namespace Dealership.Application.Sales;

// The sale decides who may see the reserved vehicle's retained photos; the shared transfer service streams them.
public interface ISaleVehiclePhotoStore
{
    Task<ListingPhotoPreparation> FindAsync(Guid actorId, Guid saleId, Guid id, CancellationToken cancellationToken);
}

public sealed class SaleVehiclePhotoService(ISaleVehiclePhotoStore photos, PhotoTransferService transfers)
{
    public async Task<ListingPhotoResult> DownloadAsync(Guid actorId, Guid saleId, Guid id,
        Stream destination, CancellationToken cancellationToken)
    {
        return ListingPhotoResult.From(await transfers.DownloadAsync(id, destination,
            token => photos.FindAsync(actorId, saleId, id, token),
            found => found.Status == ListingPhotoStatus.Available ? found.Photo : null, cancellationToken));
    }
}
