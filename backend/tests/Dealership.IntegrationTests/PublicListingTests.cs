using Dealership.Domain.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class PublicListingTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    // The reservation expiry batch needs more holds than one catalogue page, plus the withdrawn and reopened listings.
    internal const int RecordCount = 12;
    private static readonly string[] PointFields = ["code", "category", "description", "kind", "severity", "estimatedRepairCost"];

    [Fact]
    public async Task CatalogueAndRedactedDetailFollowApprovedEligibilityWithScopedCounts()
    {
        var schedule = new AppointmentSchedule(new(9, 0), new(18, 0), 15, 30, 0, Enum.GetValues<DayOfWeek>());
        using var data = await CheckInTestData.CreateAsync(fixture, schedule);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Database.OpenConnectionAsync();
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listings = services.GetRequiredService<ListingService>();
        var store = services.GetRequiredService<IPublicListingStore>();
        var records = new List<ListingRecord>();
        for (var index = 0; index < RecordCount; index++)
        {
            records.Add(await CreateAsync(data, services, manager, technician, index));
        }

        var outsideBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = outsideBranch }, CancellationToken.None);
        var outsideManager = await ProvisionAsync(services, outsideBranch, AccountRoles.BranchManager);
        var outsideTechnician = await ProvisionAsync(services, outsideBranch, AccountRoles.WorkshopTechnician);
        var outsideSales = await ProvisionAsync(services, outsideBranch, AccountRoles.SalesAgent);
        var other = await CreateAsync(data, services, outsideManager, outsideTechnician, RecordCount + 1, outsideBranch, outsideSales);

        var filter = new PublicListingFilter(BranchId: data.BranchId, Sort: "price", Direction: "asc");
        var firstPage = await store.ReadAsync(filter, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, firstPage.Status);
        Assert.Equal(PublicListingFilter.PageSize, firstPage.Catalogue!.Items.Count);
        Assert.Equal(RecordCount, firstPage.Catalogue.Total);
        Assert.Equal(RecordCount + 1, (await store.ReadAsync(new(), CancellationToken.None)).Catalogue!.Total);
        var searched = await store.ReadAsync(new(Search: "  zzz-no-such-car "), CancellationToken.None);
        Assert.Equal(0, searched.Catalogue!.Total);
        var make = await database.Vehicles.Where(row => row.Id == records[0].VehicleId).Select(row => row.Make).SingleAsync();
        var byMake = await store.ReadAsync(new(Search: make.ToUpperInvariant()), CancellationToken.None);
        Assert.Equal(RecordCount + 1, byMake.Catalogue!.Total);
        Assert.Equal(ListingStatus.Invalid, (await store.ReadAsync(new(Search: " "), CancellationToken.None)).Status);
        await ListingReadAssertions.VerifyAsync(fixture, services, data, manager, outsideManager, technician,
            records[0].VehicleId, other.VehicleId);

        using var client = data.Factory.CreateClient();
        using var listingApi = fixture.CreateFactory();
        using var listingClient = listingApi.CreateClient();
        var first = records[0];
        await PurchaseQuoteAssertions.VerifyAsync(services, client, listingClient, first.VehicleId, manager);
        using var response = await client.GetAsync($"/api/vehicles/{first.VehicleId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var detail = (await response.Content.ReadFromJsonAsync<PublicListingDetail>())!;
        Assert.Equal(await database.ListingPublications.Where(row => row.VehicleId == first.VehicleId)
            .Select(row => row.Id).SingleAsync(), detail.PublicationId);
        Assert.Equal(210, detail.Checklist.Points.Count);
        Assert.Equal(first.Draft.PhotoIds, detail.PhotoIds);
        Assert.Equal(first.Draft.PhotoIds[0], detail.Listing.CoverPhotoId);
        Assert.Equal(60000m, detail.Listing.VehiclePrice);
        Assert.Equal(90m, detail.RoadTax);
        Assert.Equal(new PurchasePrice(60000m, 500m, 90m, 40m, 0m, 60630m), detail.Price);
        Assert.Equal(0.08m, detail.SstRate);
        var json = await response.Content.ReadAsStringAsync();
        foreach (var privateValue in new[] { data.OwnerId.ToString(), first.InspectionId.ToString(), first.SignedId.ToString(),
            technician.ToString(), manager.ToString(), first.Draft.Id.ToString(), "PL1BT000000000000", "WYY 8800", "Private owner reference" })
        {
            Assert.DoesNotContain(privateValue, json, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(json);
        Assert.Equal(PointFields,
            document.RootElement.GetProperty("checklist").GetProperty("points")[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/vehicles?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/vehicles?fuel=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/vehicles?pageSize=10")).StatusCode);
        using var catalogueResponse = await client.GetAsync($"/api/vehicles?branchId={data.BranchId}");
        Assert.Equal(HttpStatusCode.OK, catalogueResponse.StatusCode);
        Assert.True(catalogueResponse.Headers.CacheControl!.NoStore);
        Assert.Equal(PublicListingFilter.PageSize, (await catalogueResponse.Content.ReadFromJsonAsync<PublicListingPage>())!.Items.Count);
        using var storefrontCatalogueResponse = await client.GetAsync($"/api/vehicles?branchId={data.BranchId}&pageSize={PublicListingFilter.StorefrontPageSize}");
        Assert.Equal(HttpStatusCode.OK, storefrontCatalogueResponse.StatusCode);
        Assert.Equal(PublicListingFilter.StorefrontPageSize, (await storefrontCatalogueResponse.Content.ReadFromJsonAsync<PublicListingPage>())!.Items.Count);

        var draft = await listings.SaveAsync(manager, first.VehicleId, new(Guid.NewGuid(), 1, 65000m, 100m,
            VehicleTransmission.Manual, VehicleFuel.Hybrid, "New description requires approval"), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        Assert.Equal(60000m, (await store.DetailAsync(first.VehicleId, CancellationToken.None)).Detail!.Listing.VehiclePrice);
        var management = services.GetRequiredService<IListingReadStore>();
        var editing = (await management.WorkspaceAsync(manager, first.VehicleId, CancellationToken.None)).Workspace!;
        Assert.Equal(draft.Draft!.Id, editing.Draft!.Id);
        Assert.Equal(first.Draft.Id, editing.ApprovedRevision!.Id);
        Assert.True(editing.Vehicle.IsPublic);
        var approved = await listings.PublishAsync(manager, first.VehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, first.SignedId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, approved.Status);
        Assert.Equal(65000m, (await store.DetailAsync(first.VehicleId, CancellationToken.None)).Detail!.Listing.VehiclePrice);
        Assert.Equal(ListingStatus.Succeeded, (await listings.WithdrawAsync(manager, first.VehicleId,
            new(Guid.NewGuid(), approved.Publication!.Id, "Correct listing specifications"), CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.NotFound, (await store.DetailAsync(first.VehicleId, CancellationToken.None)).Status);
        Assert.Equal(PurchaseQuoteStatus.NotFound, (await services.GetRequiredService<PurchaseQuoteService>()
            .QuoteAsync(first.VehicleId, "APEX500", CancellationToken.None)).Status);
        var withdrawn = (await management.WorkspaceAsync(manager, first.VehicleId, CancellationToken.None)).Workspace!;
        Assert.True(withdrawn.Vehicle.IsWithdrawn);
        Assert.False(withdrawn.Vehicle.IsPublic);
        Assert.Equal(approved.Publication.Id, withdrawn.Withdrawal!.PublicationId);
        Assert.Equal(RecordCount - 1, (await store.ReadAsync(filter, CancellationToken.None)).Catalogue!.Total);

        var reopened = await services.GetRequiredService<InspectionReopeningService>().ReopenAsync(manager, records[1].InspectionId,
            new(Guid.NewGuid(), 1, 1, "Reinspect before sale"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, reopened.Status);
        Assert.Equal(ListingStatus.NotFound, (await store.DetailAsync(records[1].VehicleId, CancellationToken.None)).Status);
        Assert.Null((await management.WorkspaceAsync(manager, records[1].VehicleId, CancellationToken.None))
            .Workspace!.CurrentSignedRevisionId);
        Assert.Null((await management.WorkspaceAsync(manager, records[1].VehicleId, CancellationToken.None))
            .Workspace!.CurrentSignedReport);
        var savedAgain = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, records[1].InspectionId,
            new(Guid.NewGuid(), 1, 1, []) { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] },
            CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, savedAgain.Status);
        var signedAgain = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician,
            records[1].InspectionId, new(Guid.NewGuid(), 1, 2, 2), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signedAgain.Status);
        Assert.Equal(VehicleStatus.Available, await database.Vehicles.Where(vehicle => vehicle.Id == records[1].VehicleId)
            .Select(vehicle => vehicle.Status).SingleAsync());
        Assert.Equal(ListingStatus.NotFound, (await store.DetailAsync(records[1].VehicleId, CancellationToken.None)).Status);
        var reinspected = (await management.WorkspaceAsync(manager, records[1].VehicleId, CancellationToken.None)).Workspace!;
        Assert.Equal(signedAgain.Report!.OperationId, reinspected.CurrentSignedRevisionId);
        Assert.Equal(signedAgain.Report.InspectionId, reinspected.CurrentSignedReport!.InspectionId);
        Assert.Equal(signedAgain.Report.Revision, reinspected.CurrentSignedReport.Revision);
        Assert.Equal(records[1].SignedId, reinspected.Publication!.SignedRevisionId);
        Assert.False(reinspected.Vehicle.IsPublic);
        await database.Branches.Where(branch => branch.Id == outsideBranch)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        Assert.Equal(ListingStatus.NotFound, (await store.DetailAsync(other.VehicleId, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.Forbidden, (await management.ReadAsync(outsideManager, new(), CancellationToken.None)).Status);
        Assert.Equal(RecordCount - 2, (await store.ReadAsync(new(), CancellationToken.None)).Catalogue!.Total);
        await ReservationCatalogueAssertions.VerifyAsync(fixture, services, data.BranchId,
            [.. records.Skip(2).Take(PublicListingFilter.PageSize + 1).Select(item => item.VehicleId)]);
    }

    private static async Task<ListingRecord> CreateAsync(CheckInTestData data, IServiceProvider services,
        Guid manager, Guid technician, int index, Guid? branchId = null, Guid? salesId = null)
    {
        var appointment = await data.BookAsync(index, $"WYY {8800 + index}", branchId: branchId);
        await data.ClaimAsync(appointment.Id, salesId);
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(salesId ?? data.StaffId,
            appointment.Id, CheckInTestData.Evidence with { Vin = $"PL1BT{index:D12}" }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var vehicleId = checkIn.Receipt!.VehicleId;
        var assigned = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            vehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspectionId = assigned.Receipt!.InspectionId;
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, 0, [new("IE-040", InspectionEntryKind.NotFitted, null, 0m, "Private owner reference")])
            { ExaminedPointCodes = [.. InspectionCatalog.Points.Where(point => point.Code != "IE-040").Select(point => point.Code)] },
            CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var signed = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician,
            inspectionId, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        var photos = new List<Guid>();
        if (index == 0)
        {
            var photo = Guid.NewGuid();
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            Assert.Equal(ListingPhotoStatus.Available, (await services.GetRequiredService<ListingPhotoService>()
                .UploadAsync(manager, vehicleId, photo, "image/jpeg", image, CancellationToken.None)).Status);
            photos.Add(photo);
        }

        var listings = services.GetRequiredService<ListingService>();
        var draft = await listings.SaveAsync(manager, vehicleId, new(Guid.NewGuid(), 0, 60000m + index % 3 * 1000m, 90m,
            VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected Malaysian vehicle")
        { PhotoIds = photos }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        Assert.Equal(ListingStatus.Succeeded, (await listings.PublishAsync(manager, vehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, signed.Report!.OperationId), CancellationToken.None)).Status);
        return new(vehicleId, inspectionId, signed.Report.OperationId, draft.Draft);
    }

    private static async Task<Guid> ProvisionAsync(IServiceProvider services, Guid branch, string role)
    {
        var id = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, $"staff.{id:N}@example.invalid", "Password123!", role, branch, "Amir Hakim", "0123456789"), CancellationToken.None);
        return id;
    }

    private sealed record ListingRecord(Guid VehicleId, Guid InspectionId, Guid SignedId, ListingRevision Draft);
}
