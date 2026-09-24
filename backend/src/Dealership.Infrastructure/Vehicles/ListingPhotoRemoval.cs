using Dealership.Domain.Common;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ListingPhotoRemoval : PhotoRemovalReceipt
{
    public Guid PhotoId { get; init; }
}
