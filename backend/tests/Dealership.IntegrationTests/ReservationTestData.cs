using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class ReservationTestData
{
    internal static async Task<Guid> RegisterCustomerAsync(IServiceProvider services, DealershipDbContext database)
    {
        var email = $"hafiz.{Guid.NewGuid():N}@example.invalid";
        var registration = new RegistrationRequest(Guid.NewGuid(), "Hafiz Rahman", email, "Password123!",
            $"920418{Random.Shared.Next(100000, 1000000)}", "0135678910", "20 Jalan Mawar, Shah Alam");
        Assert.Equal(RegistrationStatus.Succeeded, (await services.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None)).Status);
        var customerId = await database.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
        var challenge = await database.EmailVerificationChallenges.SingleAsync(item => item.UserId == customerId);
        var token = services.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(VerificationStatus.Accepted, (await services.GetRequiredService<EmailVerificationService>()
            .ConfirmAsync(token, CancellationToken.None)).Status);
        return customerId;
    }

    internal static async Task<Guid> ProvisionAsync(IServiceProvider services, Guid? branchId, string role)
    {
        var id = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, $"staff.{id:N}@example.invalid", "Password123!", role, branchId, "Amir Hakim", "0123456789"), CancellationToken.None);
        return id;
    }

    internal static async Task<ListingRecord> CreateListingAsync(CheckInTestData data, IServiceProvider services,
        Guid manager, Guid technician, int index, bool withPhoto = false, bool withInspectionEvidence = false)
    {
        var appointment = await data.BookAsync(index, $"WYY {8800 + index}");
        await data.ClaimAsync(appointment.Id);
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(data.StaffId,
            appointment.Id, CheckInTestData.Evidence with { Vin = $"PL1BT{index:D12}" }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var vehicleId = checkIn.Receipt!.VehicleId;
        var assignment = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            vehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspectionId = assignment.Receipt!.InspectionId;
        var evidence = withInspectionEvidence
            ? await InspectionEvidenceAsync(services, technician, inspectionId)
            : [];
        var saveVersion = withInspectionEvidence ? 2 : 1;
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, saveVersion - 1, withInspectionEvidence
                ? [new("EP-005", InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0m, "Surface mark")]
                : [])
            {
                PhotoIds = evidence,
                ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
            },
            CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var signed = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, saveVersion, 1), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        if (withInspectionEvidence)
        {
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionReopeningService>()
                .ReopenAsync(manager, inspectionId, new(Guid.NewGuid(), 1, 1, "Review surface condition"), default)).Status);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
                .SaveAsync(technician, inspectionId, new(Guid.NewGuid(), 1, saveVersion, [])
                { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] }, default)).Status);
            signed = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician, inspectionId,
                new(Guid.NewGuid(), 1, saveVersion + 1, 2), default);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        }

        var listings = services.GetRequiredService<ListingService>();
        var photoIds = new List<Guid>();
        if (withPhoto)
        {
            var photoId = Guid.NewGuid();
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            var upload = await services.GetRequiredService<ListingPhotoService>().UploadAsync(manager, vehicleId,
                photoId, "image/jpeg", image, CancellationToken.None);
            Assert.Equal(ListingPhotoStatus.Available, upload.Status);
            photoIds.Add(photoId);
        }

        var draft = await listings.SaveAsync(manager, vehicleId, new(Guid.NewGuid(), 0, 60000m, 90m,
            VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected Malaysian vehicle")
        {
            PhotoIds = photoIds
        }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        var published = await listings.PublishAsync(manager, vehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, signed.Report!.OperationId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        return new(inspectionId, draft.Draft, published.Publication!);
    }

    internal sealed record ListingRecord(Guid InspectionId, ListingRevision Revision, ListingPublication Publication);

    private static async Task<Guid[]> InspectionEvidenceAsync(IServiceProvider services, Guid technician, Guid inspection)
    {
        var photos = new List<Guid>();
        foreach (var point in new[] { "EP-004", "EP-005", "EP-006" })
        {
            var id = Guid.NewGuid();
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            var upload = await services.GetRequiredService<InspectionPhotoService>().UploadAsync(technician,
                inspection, id, 1, point, "image/jpeg", image, default);
            Assert.Equal(InspectionPhotoStatus.Available, upload.Status);
            photos.Add(id);
        }

        // A retained unsigned save and an unattached upload must not become customer report evidence.
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspection,
            new(Guid.NewGuid(), 1, 0,
                [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0m, "Earlier observation")])
            { PhotoIds = [photos[0]] }, default);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        return [photos[1]];
    }

    // Records a refund the way the services do: the receipt first, then the sale it closes, in one transaction.
    internal static async Task<ReservationRelease> ReleaseAsync(DealershipDbContext database, Reservation reservation,
        ReservationReleaseKind kind, Guid actorId, string? reason, DateTimeOffset now)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        var sale = await database.SalesOrders.SingleAsync(item => item.Id == reservation.Id);
        var release = new ReservationRelease(Guid.NewGuid(), reservation, sale.DueAt, kind, actorId, reason, now);
        database.ReservationReleases.Add(release);
        await database.SaveChangesAsync();
        database.SalesOrderEvents.Add(kind == ReservationReleaseKind.CustomerCancelled
            ? sale.CancelByCustomer(Guid.NewGuid(), actorId, release.Id, now)
            : sale.Cancel(Guid.NewGuid(), actorId, reason!, release.Id, now));
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return release;
    }
}
