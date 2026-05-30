using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentListingFixtures(DealershipDbContext database, ListingService listings,
    ListingPhotoService photos, IPhotoImageValidator validator)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentInspectionScenarios.All.Where(item => item.PublishListing))
        {
            reports.Add(await RunOneAsync(seed, scenario, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentInspectionScenario scenario,
        CancellationToken cancellationToken)
    {
        var label = $"Listing ({scenario.Intake.RegistrationPlate})";
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntakeAppointmentId == scenario.Id, cancellationToken);
        if (vehicle is null)
        {
            return new(label, "checked-in vehicle missing; preserved", true);
        }

        var expectedSign = scenario.Sign(scenario.Stage == DevelopmentInspectionStage.Reinspected);
        var settled = (vehicle.Status == VehicleStatus.Sold || vehicle.Status == VehicleStatus.Delivered) && await database.SalesOrders.AnyAsync(item =>
            item.VehicleId == vehicle.Id && (item.Status == SalesOrderStatus.Settled || item.Status == SalesOrderStatus.Delivered), cancellationToken);
        var currentInspection = await database.InspectionDrafts.AnyAsync(item => item.Id == scenario.Id &&
            item.VehicleId == vehicle.Id && item.IsSigned && item.Revision == expectedSign.Revision &&
            item.AssignmentVersion == 1, cancellationToken);
        if (!currentInspection || vehicle.Status != VehicleStatus.Available && !settled ||
            !await database.InspectionSignedRevisions.AnyAsync(item => item.Id == expectedSign.OperationId &&
                item.InspectionId == scenario.Id && item.Revision == expectedSign.Revision, cancellationToken))
        {
            return new(label, "inspection drift; preserved", true);
        }

        var manager = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == vehicle.BranchId && item.Role == AccountRoles.BranchManager);
        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleAsync(item => item.Id == scenario.Id, cancellationToken);
        var details = DevelopmentVehicleCatalogue.For(vehicle.Make, vehicle.Model, vehicle.Trim);
        var request = new ListingDraftRequest(scenario.OperationId("listing-draft"), 0,
            appointment.Valuation.Maximum, details.RoadTax, VehicleTransmission.Automatic, details.Fuel,
            $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} {vehicle.Trim}. " +
            details.Description + " " +
            "View the signed inspection checklist for the recorded vehicle condition.")
        {
            PhotoIds = [.. new[] { PhotoAngle.Front, PhotoAngle.Rear, PhotoAngle.Driver, PhotoAngle.Passenger }.Select(angle =>
                scenario.OperationId($"listing-photo-{DevelopmentFixturePhotos.MediaVersion}-{angle}"))]
        };
        var existing = await database.ListingRevisions.AsNoTracking()
            .Where(item => item.VehicleId == vehicle.Id).OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && (existing.Id != request.OperationId || existing.Version != 1 ||
            existing.ActorId != manager.Id || existing.VehiclePrice != request.VehiclePrice ||
            existing.RoadTax != request.RoadTax || existing.Description != request.Description ||
            existing.Transmission != request.Transmission || existing.Fuel != request.Fuel ||
            !existing.PhotoIds.SequenceEqual(request.PhotoIds)))
        {
            return new(label, "listing drift; preserved", true);
        }

        if (!await PhotosAsync(seed, scenario, vehicle.Id, manager.Id, cancellationToken))
        {
            return new(label, "listing photos missing/changed; preserved", true);
        }

        if (existing is null && seed)
        {
            var saved = await listings.SaveAsync(manager.Id, vehicle.Id, request, cancellationToken);
            if (saved.Status != ListingStatus.Succeeded)
            {
                return new(label, "draft unavailable; preserved", true);
            }

            existing = saved.Draft;
        }

        if (existing is null)
        {
            return new(label, "draft missing", true);
        }

        var signedId = expectedSign.OperationId;
        var publicationId = scenario.OperationId("listing-publication");
        var published = await database.ListingPublications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RevisionId == existing.Id, cancellationToken);
        if (published is not null)
        {
            var withdrawal = await database.ListingWithdrawals.AsNoTracking()
                .SingleOrDefaultAsync(item => item.PublicationId == published.Id, cancellationToken);
            var expectedWithdrawal = withdrawal is not null && scenario.Id == DevelopmentTestDriveWithdrawalFixtures.Scenario.Id &&
                withdrawal.Id == DevelopmentTestDriveWithdrawalFixtures.WithdrawalId && withdrawal.ActorId == manager.Id &&
                withdrawal.Reason == DevelopmentTestDriveWithdrawalFixtures.Reason;
            var matches = published.Id == publicationId && published.ActorId == manager.Id &&
                published.SignedRevisionId == signedId && (withdrawal is null || expectedWithdrawal) &&
                (vehicle.Status == VehicleStatus.Available || settled);
            return new(label, matches ? "matches" : "publication drift; preserved", !matches);
        }

        if (!seed)
        {
            return new(label, "publication missing", true);
        }

        var result = await listings.PublishAsync(manager.Id, vehicle.Id, new(publicationId, existing.Id, signedId), cancellationToken);
        var created = result.Status == ListingStatus.Succeeded && result.Publication!.SignedRevisionId == signedId;
        return new(label, created ? "created" : "publication unavailable; preserved", !created);
    }

    private async Task<bool> PhotosAsync(bool seed, DevelopmentInspectionScenario scenario, Guid vehicleId,
        Guid managerId, CancellationToken cancellationToken)
    {
        var vehicle = scenario.Intake.Vehicle;
        var details = DevelopmentVehicleCatalogue.For(vehicle.Make!, vehicle.Model!, vehicle.Trim);
        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var id = scenario.OperationId($"listing-photo-{DevelopmentFixturePhotos.MediaVersion}-{angle}");
            var contentType = details.ContentType(angle);
            using var content = DevelopmentFixturePhotos.Open(angle, details);
            var validated = await validator.ValidateAsync(content, contentType, cancellationToken)
                ?? throw new InvalidOperationException("The listing fixture photo must pass canonical image validation.");
            var existing = await database.ListingPhotos.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (existing is not null && !existing.Matches(vehicleId, managerId, validated))
            {
                return false;
            }

            if (existing?.AvailableAt is not null)
            {
                continue;
            }

            if (!seed)
            {
                return false;
            }

            content.Position = 0;
            var result = await photos.UploadAsync(managerId, vehicleId, id, contentType, content, cancellationToken);
            if (result.Status != ListingPhotoStatus.Available)
            {
                return false;
            }
        }

        return true;
    }
}
