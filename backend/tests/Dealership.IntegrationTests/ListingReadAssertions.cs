using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class ListingReadAssertions
{
    public static async Task VerifyAsync(IntakePhotoFixture fixture, IServiceProvider services, CheckInTestData data, Guid manager,
        Guid outsideManager, Guid technician, Guid vehicleId, Guid outsideVehicle)
    {
        var store = services.GetRequiredService<IListingReadStore>();
        var database = services.GetRequiredService<DealershipDbContext>();
        var group = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(group, $"staff.{group:N}@example.invalid", "Password123!", AccountRoles.GeneralManager, null, "Amir Hakim", "0123456789"), CancellationToken.None);
        var filter = new ListingReadFilter(PageSize: 10);
        var first = await store.ReadAsync(manager, filter, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, first.Status);
        Assert.Equal(PublicListingTests.RecordCount, first.Listings!.Total);
        Assert.Equal(10, first.Listings.Items.Count);
        Assert.All(first.Listings.Items, item => Assert.Equal(data.BranchId, item.BranchId));
        Assert.Equal(PublicListingTests.RecordCount + 1, (await store.ReadAsync(group, new(), CancellationToken.None)).Listings!.Total);
        Assert.Equal(PublicListingTests.RecordCount, (await store.ReadAsync(group, new(BranchId: data.BranchId), CancellationToken.None)).Listings!.Total);
        var outsideBranch = await database.Users.Where(user => user.Id == outsideManager).Select(user => user.BranchId).SingleAsync();
        Assert.Equal(ListingStatus.Forbidden, (await store.ReadAsync(manager, new(BranchId: outsideBranch), CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.NotFound, (await store.WorkspaceAsync(manager, outsideVehicle, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.NotFound, (await store.PhotosAsync(manager, outsideVehicle, 1, 10, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.Succeeded, (await store.WorkspaceAsync(group, outsideVehicle, CancellationToken.None)).Status);
        foreach (var denied in new[] { technician, data.StaffId, data.OwnerId, Guid.NewGuid() })
        {
            Assert.Equal(ListingStatus.Forbidden, (await store.ReadAsync(denied, filter, CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.Forbidden, (await store.WorkspaceAsync(denied, vehicleId, CancellationToken.None)).Status);
            Assert.Equal(ListingStatus.Forbidden, (await store.PhotosAsync(denied, vehicleId, 1, 10, CancellationToken.None)).Status);
        }

        Assert.Equal(ListingStatus.Invalid, (await store.ReadAsync(manager, new(PageSize: 8), CancellationToken.None)).Status);
        Assert.Empty((await store.ReadAsync(manager, new(Search: "%"), CancellationToken.None)).Listings!.Items);
        Assert.Single((await store.ReadAsync(manager, new(Search: "WYY 8800"), CancellationToken.None)).Listings!.Items);
        // The table shows the year, so search finds it; the VIN is searchable and the branch name is for the group scope only.
        var searched = (await store.ReadAsync(manager, new(Search: "WYY 8800"), CancellationToken.None)).Listings!.Items[0];
        Assert.Contains(searched.VehicleId, (await store.ReadAsync(manager, new(Search: $"{searched.ManufactureYear} {searched.Make}"),
            CancellationToken.None)).Listings!.Items.Select(item => item.VehicleId));
        var searchedVin = await database.Vehicles.Where(vehicle => vehicle.Id == searched.VehicleId).Select(vehicle => vehicle.Vin).SingleAsync();
        Assert.Contains(searched.VehicleId, (await store.ReadAsync(manager, new(Search: searchedVin), CancellationToken.None))
            .Listings!.Items.Select(item => item.VehicleId));
        Assert.Empty((await store.ReadAsync(manager, new(Search: searched.BranchName), CancellationToken.None)).Listings!.Items);
        Assert.NotEmpty((await store.ReadAsync(group, new(Search: searched.BranchName), CancellationToken.None)).Listings!.Items);
        var workspace = (await store.WorkspaceAsync(manager, vehicleId, CancellationToken.None)).Workspace!;
        Assert.Equal(workspace.Draft!.Id, workspace.ApprovedRevision!.Id);
        Assert.Equal(workspace.Publication!.SignedRevisionId, workspace.CurrentSignedRevisionId);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == workspace.CurrentSignedRevisionId);
        Assert.Equal(signed.InspectionId, workspace.CurrentSignedReport!.InspectionId);
        Assert.Equal(signed.Revision, workspace.CurrentSignedReport.Revision);
        Assert.True(workspace.Vehicle.IsPublic);
        Assert.False(string.IsNullOrEmpty(workspace.Vin));
        Assert.Null(workspace.Hold);
        var matched = (await store.ReadAsync(manager, new(Registration: workspace.Vehicle.RegistrationPlate,
            Vehicle: $"{workspace.Vehicle.ManufactureYear} {workspace.Vehicle.Make}",
            MinimumPublishedPrice: workspace.Vehicle.ApprovedVehiclePrice,
            MaximumPublishedPrice: workspace.Vehicle.ApprovedVehiclePrice), CancellationToken.None)).Listings!;
        Assert.Equal(1, matched.Total);
        Assert.Equal(vehicleId, Assert.Single(matched.Items).VehicleId);
        Assert.Empty((await store.ReadAsync(manager, new(Registration: "%"), CancellationToken.None)).Listings!.Items);
        Assert.Empty((await store.ReadAsync(manager, new(Registration: workspace.Vehicle.RegistrationPlate,
            MinimumPublishedPrice: workspace.Vehicle.ApprovedVehiclePrice + 1),
            CancellationToken.None)).Listings!.Items);
        Assert.False(workspace.Vehicle.IsWithdrawn);
        Assert.Null(workspace.Withdrawal);
        await VerifyPhotosAsync(services, store, manager, vehicleId);

        using var api = fixture.CreateFactory();
        using var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/staff/listings")).StatusCode);
        var email = await database.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await client.GetAsync("/api/staff/listings?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(PublicListingTests.RecordCount, (await response.Content.ReadFromJsonAsync<ListingManagerPage>())!.Total);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/staff/listings/{vehicleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/staff/listings/{vehicleId}/photos?pageSize=10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?pageSize=8")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?sort=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?sort=price&direction=sideways")).StatusCode);

        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, outsideBranch));
        Assert.Equal(1, (await store.ReadAsync(manager, new(), CancellationToken.None)).Listings!.Total);
        Assert.Equal(ListingStatus.NotFound, (await store.WorkspaceAsync(manager, vehicleId, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.NotFound, (await store.PhotosAsync(manager, vehicleId, 1, 10, CancellationToken.None)).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.BranchId, data.BranchId).SetProperty(user => user.Role, AccountRoles.SalesAgent));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/listings")).StatusCode);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.IsActive, false));
        Assert.Equal(ListingStatus.Forbidden, (await store.ReadAsync(manager, new(), CancellationToken.None)).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));

        await database.Users.Where(user => user.Id == group).ExecuteUpdateAsync(update => update.SetProperty(user => user.Role, AccountRoles.Administrator));
        Assert.Equal(ListingStatus.Forbidden, (await store.ReadAsync(group, new(), CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.Forbidden, (await store.WorkspaceAsync(group, vehicleId, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.Forbidden, (await store.PhotosAsync(group, vehicleId, 1, 10, CancellationToken.None)).Status);
        await database.Users.Where(user => user.Id == group).ExecuteUpdateAsync(update => update.SetProperty(user => user.Role, AccountRoles.GeneralManager));

        var appointment = await data.BookAsync(19, "WYY 8819");
        await data.ClaimAsync(appointment.Id);
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(data.StaffId, appointment.Id,
            CheckInTestData.Evidence with { Vin = "PL1BT000000000019" }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var unlisted = (await store.WorkspaceAsync(manager, checkIn.Receipt!.VehicleId, CancellationToken.None)).Workspace!;
        Assert.Null(unlisted.Draft);
        Assert.Null(unlisted.ApprovedRevision);
        Assert.Null(unlisted.Publication);
        Assert.Null(unlisted.CurrentSignedRevisionId);
        Assert.Null(unlisted.CurrentSignedReport);
        Assert.Equal(0, unlisted.Vehicle.Version);
        Assert.Null(unlisted.Vehicle.RevisionId);
        Assert.False(unlisted.Vehicle.IsPublic);
        Assert.Contains((await store.ReadAsync(manager, new(), CancellationToken.None)).Listings!.Items,
            item => item.VehicleId == checkIn.Receipt.VehicleId && item.Version == 0);
        Assert.Empty((await store.PhotosAsync(manager, checkIn.Receipt.VehicleId, 1, 10, CancellationToken.None)).Photos!.Items);
    }

    private static async Task VerifyPhotosAsync(IServiceProvider services, IListingReadStore store, Guid manager, Guid vehicleId)
    {
        for (var index = 0; index < 2; index++)
        {
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            Assert.Equal(ListingPhotoStatus.Available, (await services.GetRequiredService<ListingPhotoService>()
                .UploadAsync(manager, vehicleId, Guid.NewGuid(), "image/jpeg", image, CancellationToken.None)).Status);
        }

        var first = (await store.PhotosAsync(manager, vehicleId, 1, 10, CancellationToken.None)).Photos!;
        Assert.Equal(3, first.Total);
        Assert.Equal(3, first.Items.Count);
        Assert.All(first.Items, item => Assert.NotNull(item.AvailableAt));
    }
}
