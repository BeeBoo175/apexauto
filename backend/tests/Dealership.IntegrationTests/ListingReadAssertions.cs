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
            new(group, $"staff.{group:N}@example.invalid", "Password123!", AccountRoles.GeneralManager, null), CancellationToken.None);
        var filter = new ListingReadFilter(PageSize: 10);
        var first = await store.ReadAsync(manager, filter, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, first.Status);
        Assert.Equal(17, first.Listings!.Total);
        Assert.Equal(10, first.Listings.Items.Count);
        Assert.All(first.Listings.Items, item => Assert.Equal(data.BranchId, item.BranchId));
        var second = (await store.ReadAsync(manager, filter with { Page = 2 }, CancellationToken.None)).Listings!;
        Assert.Equal(7, second.Items.Count);
        Assert.Equal(17, first.Listings.Items.Concat(second.Items).Select(item => item.VehicleId).Distinct().Count());
        Assert.Equal(18, (await store.ReadAsync(group, new(), CancellationToken.None)).Listings!.Total);
        Assert.Equal(17, (await store.ReadAsync(group, new(BranchId: data.BranchId), CancellationToken.None)).Listings!.Total);
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
        Assert.Empty((await store.ReadAsync(manager, new(Page: int.MaxValue), CancellationToken.None)).Listings!.Items);
        var workspace = (await store.WorkspaceAsync(manager, vehicleId, CancellationToken.None)).Workspace!;
        Assert.Equal(workspace.Draft!.Id, workspace.ApprovedRevision!.Id);
        Assert.Equal(workspace.Publication!.SignedRevisionId, workspace.CurrentSignedRevisionId);
        Assert.True(workspace.Vehicle.IsPublic);
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
        Assert.Equal(17, (await response.Content.ReadFromJsonAsync<ListingManagerPage>())!.Total);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/staff/listings/{vehicleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/staff/listings/{vehicleId}/photos?pageSize=10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?pageSize=8")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?sort=unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/staff/listings?sort=price&direction=sideways")).StatusCode);
        var sorted = await client.GetFromJsonAsync<ListingManagerPage>("/api/staff/listings?sort=registration&direction=desc&pageSize=10");
        Assert.Equal(first.Listings.Items.Concat(second.Items).OrderByDescending(item => item.RegistrationPlate)
            .ThenBy(item => item.VehicleId).Take(10).Select(item => item.VehicleId), sorted!.Items.Select(item => item.VehicleId));

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
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(data.StaffId, appointment.Id,
            CheckInTestData.Evidence with { Vin = "PL1BT000000000019" }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var unlisted = (await store.WorkspaceAsync(manager, checkIn.Receipt!.VehicleId, CancellationToken.None)).Workspace!;
        Assert.Null(unlisted.Draft);
        Assert.Null(unlisted.ApprovedRevision);
        Assert.Null(unlisted.Publication);
        Assert.Null(unlisted.CurrentSignedRevisionId);
        Assert.Equal(0, unlisted.Vehicle.Version);
        Assert.Null(unlisted.Vehicle.RevisionId);
        Assert.False(unlisted.Vehicle.IsPublic);
        Assert.Contains((await store.ReadAsync(manager, new(), CancellationToken.None)).Listings!.Items,
            item => item.VehicleId == checkIn.Receipt.VehicleId && item.Version == 0);
        Assert.Empty((await store.PhotosAsync(manager, checkIn.Receipt.VehicleId, 1, 10, CancellationToken.None)).Photos!.Items);
        await VerifySortingAsync(store, group);
    }

    private static async Task VerifySortingAsync(IListingReadStore store, Guid manager)
    {
        var all = (await store.ReadAsync(manager, new(PageSize: 50), CancellationToken.None)).Listings!.Items;
        foreach (var sort in new[] { "registration", "vehicle", "branch", "inspection", "price", "publication" })
        {
            await VerifySortAsync(store, manager, all, sort, "asc");
            await VerifySortAsync(store, manager, all, sort, "desc");
        }
    }

    private static async Task VerifySortAsync(IListingReadStore store, Guid manager,
        IReadOnlyList<ListingManagerItem> all, string sort, string direction)
    {
        Func<ListingManagerItem, IComparable?> key = sort switch
        {
            "registration" => item => item.RegistrationPlate,
            "vehicle" => item => $"{item.ManufactureYear} {item.Make} {item.Model} {item.Trim}",
            "branch" => item => item.BranchName,
            "inspection" => item => item.VehicleStatus.ToString(),
            "price" => item => item.VehiclePrice,
            _ => item => PublicationOrder(item)
        };
        IEnumerable<ListingManagerItem> expected = direction == "desc" ? all.OrderByDescending(key) : all.OrderBy(key);
        if (sort == "price")
        {
            expected = expected.OrderBy(item => item.VehiclePrice is null);
        }

        var ordered = expected.GroupBy(key).SelectMany(group => group.OrderBy(item => item.VehicleId)).ToArray();
        var filter = new ListingReadFilter(PageSize: 10, Sort: sort, Direction: direction);
        var first = (await store.ReadAsync(manager, filter, CancellationToken.None)).Listings!;
        var second = (await store.ReadAsync(manager, filter with { Page = 2 }, CancellationToken.None)).Listings!;
        Assert.Equal(all.Count, first.Total);
        Assert.Equal(all.Count, second.Total);
        var actual = first.Items.Concat(second.Items).ToArray();
        Assert.True(ordered.Select(item => item.VehicleId).SequenceEqual(actual.Select(item => item.VehicleId)),
            $"{sort} {direction}: expected {string.Join(", ", ordered.Select(item => item.RegistrationPlate))}; " +
            $"actual {string.Join(", ", actual.Select(item => item.RegistrationPlate))}");
    }

    private static int PublicationOrder(ListingManagerItem item)
    {
        if (item.IsPublic)
        {
            return item.RevisionId == item.PublishedRevisionId ? 3 : 4;
        }

        if (item.IsWithdrawn)
        {
            return 5;
        }

        if (item.PublicationId is not null)
        {
            return 2;
        }

        return item.RevisionId is not null ? 1 : 0;
    }

    private static async Task VerifyPhotosAsync(IServiceProvider services, IListingReadStore store, Guid manager, Guid vehicleId)
    {
        for (var index = 0; index < 11; index++)
        {
            using var image = new MemoryStream(IntakePhotoFixture.Image());
            Assert.Equal(ListingPhotoStatus.Available, (await services.GetRequiredService<ListingPhotoService>()
                .UploadAsync(manager, vehicleId, Guid.NewGuid(), "image/jpeg", image, CancellationToken.None)).Status);
        }

        var first = (await store.PhotosAsync(manager, vehicleId, 1, 10, CancellationToken.None)).Photos!;
        var second = (await store.PhotosAsync(manager, vehicleId, 2, 10, CancellationToken.None)).Photos!;
        Assert.Equal(12, first.Total);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(12, first.Items.Concat(second.Items).Select(item => item.Id).Distinct().Count());
        Assert.All(first.Items, item => Assert.NotNull(item.AvailableAt));
        Assert.Empty((await store.PhotosAsync(manager, vehicleId, int.MaxValue, 10, CancellationToken.None)).Photos!.Items);
    }
}
