using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Builds the published listing of a signed vehicle at its historical times inside the snapshot transaction,
// mirroring the canonical photo upload, draft save and publication commands, which can only stamp the database clock.
public sealed class DevelopmentListingHistory(DealershipDbContext database, IPhotoImageValidator validator,
    IPhotoStorage storage)
{
    private static readonly TimeSpan PhotoDelay = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AvailableDelay = TimeSpan.FromMinutes(31);
    private static readonly TimeSpan DraftDelay = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan PublicationDelay = TimeSpan.FromMinutes(90);

    public static DateTimeOffset PublishedAt(DateTimeOffset signedAt) => signedAt + PublicationDelay;

    public async Task AddPublishedListingAsync(DevelopmentInspectionScenario scenario, DateTimeOffset signedAt,
        CancellationToken cancellationToken)
    {
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.IntakeAppointmentId == scenario.Id, cancellationToken);
        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.Id, cancellationToken);
        var managerId = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == vehicle.BranchId && item.Role == AccountRoles.BranchManager).Id;
        await AddPhotosAsync(scenario, vehicle, managerId, signedAt, cancellationToken);

        var request = DevelopmentListingFixtures.DraftRequest(scenario, vehicle, appointment);
        var revision = new ListingRevision(request.OperationId, vehicle.Id, vehicle.BranchId, managerId, 1,
            request.VehiclePrice, request.RoadTax, request.Transmission, request.Fuel, request.Description!,
            signedAt + DraftDelay, request.PhotoIds);
        database.ListingRevisions.Add(revision);
        database.ListingRevisionPhotos.AddRange(request.PhotoIds!.Select((id, index) =>
            new ListingRevisionPhoto(revision.Id, id, vehicle.Id, vehicle.BranchId, index)));
        await database.SaveChangesAsync(cancellationToken);

        database.ListingPublications.Add(new ListingPublication(scenario.OperationId("listing-publication"), revision,
            scenario.Sign().OperationId, managerId, PublishedAt(signedAt)));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task AddPhotosAsync(DevelopmentInspectionScenario scenario, Vehicle vehicle, Guid managerId,
        DateTimeOffset signedAt, CancellationToken cancellationToken)
    {
        var details = DevelopmentVehicleCatalogue.For(vehicle.Make, vehicle.Model, vehicle.Trim);
        var photos = new List<ListingPhoto>();
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var contentType = details.ContentType(angle);
            using var content = DevelopmentFixturePhotos.Open(angle, details);
            var validated = await validator.ValidateAsync(content, contentType, cancellationToken)
                ?? throw new InvalidOperationException("The listing fixture photo must pass canonical image validation.");
            var photo = new ListingPhoto(
                scenario.OperationId($"listing-photo-{DevelopmentFixturePhotos.MediaVersion}-{angle}"),
                vehicle.Id, vehicle.BranchId, managerId, validated, signedAt + PhotoDelay);
            content.Position = 0;
            await storage.PutAsync(photo, content, cancellationToken);
            photos.Add(photo);
        }

        database.ListingPhotos.AddRange(photos);
        await database.SaveChangesAsync(cancellationToken);
        foreach (var photo in photos)
        {
            photo.ConfirmAvailable(signedAt + AvailableDelay);
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
