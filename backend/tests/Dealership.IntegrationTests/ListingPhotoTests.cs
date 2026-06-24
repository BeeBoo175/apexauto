using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SkiaSharp;

namespace Dealership.IntegrationTests;

public sealed class ListingPhotoTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SeparatePhotosRetainOrderedApprovalAndExactReceiptsWithoutLeakingPrivateOrWithdrawnMedia()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        var checkedIn = await data.SaveBundleAsync(appointment.Id);
        var vehicleId = checkedIn.VehicleId;
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var (manager, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (technician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, otherBranch, AccountRoles.BranchManager);
        var photoId = Guid.NewGuid();
        var bytes = IntakePhotoFixture.Image();

        Assert.Equal(ListingPhotoStatus.Forbidden, (await UploadAsync(data.Factory, data.StaffId, vehicleId, photoId, bytes)).Status);
        Assert.Equal(ListingPhotoStatus.NotFound, (await UploadAsync(data.Factory, outside, vehicleId, photoId, bytes)).Status);
        using (var failed = fixture.CreateFactory(failAfterPut: true))
        {
            await Assert.ThrowsAsync<PhotoStorageException>(() => UploadAsync(failed, manager, vehicleId, photoId, bytes));
        }

        Assert.Equal(ListingPhotoStatus.Pending, (await services.GetRequiredService<ListingPhotoService>()
            .ReceiptAsync(manager, vehicleId, photoId, CancellationToken.None)).Status);
        await VerifyConfirmationOrderAsync(data.Factory, manager, vehicleId, photoId);
        var uploads = await Task.WhenAll(UploadAsync(data.Factory, manager, vehicleId, photoId, bytes),
            UploadAsync(data.Factory, manager, vehicleId, photoId, bytes));
        Assert.All(uploads, result => Assert.Equal(ListingPhotoStatus.Available, result.Status));
        Assert.Equal(uploads[0].Receipt, uploads[1].Receipt);
        using var restarted = fixture.CreateFactory();
        Assert.Equal(uploads[0].Receipt, (await UploadAsync(restarted, manager, vehicleId, photoId, bytes)).Receipt);
        Assert.Equal(ListingPhotoStatus.Conflict,
            (await UploadAsync(restarted, manager, vehicleId, photoId, IntakePhotoFixture.Image(color: SKColors.Red))).Status);
        Assert.Equal(ListingPhotoStatus.Conflict, (await UploadAsync(restarted, group, vehicleId, photoId, bytes)).Status);

        using var client = restarted.CreateClient();
        var staffPath = $"/api/staff/listings/{vehicleId}/photos/{photoId}";
        var publicPath = $"/api/vehicles/{vehicleId}/photos/{photoId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(staffPath)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync(staffPath, body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(staffPath + "/receipt")).StatusCode);
        Assert.Equal(bytes, await client.GetByteArrayAsync(staffPath));
        var secondId = Guid.NewGuid();
        Assert.Equal(ListingPhotoStatus.Available, (await UploadAsync(restarted, group, vehicleId, secondId, bytes)).Status);

        var request = new ListingDraftRequest(Guid.NewGuid(), 0, 60000m, 90m,
            VehicleTransmission.Automatic, VehicleFuel.Petrol, "Separate approved storefront photos")
        {
            PhotoIds = [photoId, secondId]
        };
        var listings = services.GetRequiredService<ListingService>();
        var draft = await listings.SaveAsync(manager, vehicleId, request, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        Assert.Equal(request.PhotoIds, draft.Draft!.PhotoIds);
        Assert.Equal(ListingStatus.Succeeded, (await listings.SaveAsync(manager, vehicleId, request, CancellationToken.None)).Status);
        Assert.Equal(ListingStatus.Conflict, (await listings.SaveAsync(manager, vehicleId,
            request with { PhotoIds = [secondId, photoId] }, CancellationToken.None)).Status);
        var privateId = await database.AppointmentPhotos.Where(item => item.AppointmentId == appointment.Id)
            .Select(item => item.PhotoId).FirstAsync();
        Assert.Equal(ListingStatus.Conflict, (await listings.SaveAsync(manager, vehicleId,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, PhotoIds = [privateId] }, CancellationToken.None)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(publicPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/vehicles/{vehicleId}/photos/{privateId}")).StatusCode);

        var assigned = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            vehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspectionId = assigned.Receipt!.InspectionId;
        await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, 0, []) { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] }, CancellationToken.None);
        var signed = await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, inspectionId, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None);
        var publication = await listings.PublishAsync(manager, vehicleId,
            new(Guid.NewGuid(), draft.Draft.Id, signed.Report!.OperationId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, publication.Status);
        using var response = await client.GetAsync(publicPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));

        var next = await listings.SaveAsync(manager, vehicleId,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, PhotoIds = [secondId] }, CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, next.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(publicPath)).StatusCode);
        var approved = await listings.PublishAsync(group, vehicleId,
            new(Guid.NewGuid(), next.Draft!.Id, signed.Report.OperationId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, approved.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(publicPath)).StatusCode);
        await VerifySqlAsync(photoId, secondId, draft.Draft.Id, next.Draft.Id);

        using var withdrawing = fixture.CreateFactory(observeDownload: _ =>
        {
            using var writer = restarted.Services.CreateScope();
            var result = writer.ServiceProvider.GetRequiredService<ListingService>().WithdrawAsync(manager, vehicleId,
                new(Guid.NewGuid(), approved.Publication!.Id, "Withdraw during provider read"), CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(ListingStatus.Succeeded, result.Status);
        });
        using var racingClient = withdrawing.CreateClient();
        var withheld = await racingClient.GetAsync($"/api/vehicles/{vehicleId}/photos/{secondId}");
        Assert.Equal(HttpStatusCode.NotFound, withheld.StatusCode);
        Assert.Empty(await withheld.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(staffPath)).StatusCode);
    }

    private async Task VerifyConfirmationOrderAsync(WebApplicationFactory<Program> factory, Guid actor, Guid vehicle, Guid photo)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await database.Database.OpenConnectionAsync();
        var pid = await database.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var held = await connection.BeginTransactionAsync();
        await using var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, held);
        advisory.Parameters.AddWithValue("key", "listing-photo:" + photo);
        await advisory.ExecuteNonQueryAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var confirmation = scope.ServiceProvider.GetRequiredService<IListingPhotoStore>()
            .ConfirmAsync(actor, vehicle, photo, deadline.Token);
        var committed = false;
        try
        {
            await using var waiting = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted AND locktype = 'advisory')", connection, held);
            waiting.Parameters.AddWithValue("pid", pid);
            var blocked = false;
            for (var attempt = 0; attempt < 100 && !blocked; attempt++)
            {
                await Task.Delay(20, deadline.Token);
                blocked = await waiting.ExecuteScalarAsync(deadline.Token) is true;
            }

            Assert.True(blocked, "Confirmation must wait for the upload identity before taking vehicle locks.");
            await using var vehicleLock = new NpgsqlCommand("SELECT \"Id\" FROM \"Vehicles\" WHERE \"Id\" = @id FOR UPDATE NOWAIT", connection, held);
            vehicleLock.Parameters.AddWithValue("id", vehicle);
            Assert.Equal(vehicle, await vehicleLock.ExecuteScalarAsync(deadline.Token));
            await held.CommitAsync(deadline.Token);
            committed = true;
            Assert.Equal(ListingPhotoStatus.Available, (await confirmation).Status);
        }
        finally
        {
            if (!committed)
            {
                await held.RollbackAsync();
            }

            await confirmation;
        }
    }

    private async Task VerifySqlAsync(Guid photoId, Guid secondId, Guid revisionId, Guid latestRevisionId)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var update = new NpgsqlCommand("UPDATE \"ListingPhotos\" SET \"Sha256\" = repeat('a', 64) WHERE \"Id\" = @id", connection);
        update.Parameters.AddWithValue("id", photoId);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal("CK_ListingPhotos_Immutable", immutable.ConstraintName);
        await using var reorder = new NpgsqlCommand("UPDATE \"ListingRevisionPhotos\" SET \"Position\" = 9 WHERE \"RevisionId\" = @id", connection);
        reorder.Parameters.AddWithValue("id", revisionId);
        var ordered = await Assert.ThrowsAsync<PostgresException>(() => reorder.ExecuteNonQueryAsync());
        Assert.Equal("CK_ListingRevisionPhotos_Immutable", ordered.ConstraintName);
        await using var append = new NpgsqlCommand("""
            INSERT INTO "ListingRevisionPhotos" ("RevisionId", "PhotoId", "VehicleId", "BranchId", "Position")
            SELECT "Id", @photo, "VehicleId", "BranchId", 1 FROM "ListingRevisions" WHERE "Id" = @id
            """, connection);
        append.Parameters.AddWithValue("photo", photoId);
        append.Parameters.AddWithValue("id", latestRevisionId);
        var undeclared = await Assert.ThrowsAsync<PostgresException>(() => append.ExecuteNonQueryAsync());
        Assert.Equal("CK_ListingRevisionPhotos_Available", undeclared.ConstraintName);
        await using var incomplete = new NpgsqlCommand("""
            INSERT INTO "ListingRevisions" ("Id", "VehicleId", "BranchId", "ActorId", "Version", "VehiclePrice",
                "RoadTax", "Transmission", "Fuel", "Description", "RecordedAt", "PhotoIds")
            SELECT @operation, "VehicleId", "BranchId", "ActorId", "Version" + 1, "VehiclePrice",
                "RoadTax", "Transmission", "Fuel", "Description", clock_timestamp(), ARRAY[@photo]
            FROM "ListingRevisions" WHERE "Id" = @id
            """, connection);
        incomplete.Parameters.AddWithValue("operation", Guid.NewGuid());
        incomplete.Parameters.AddWithValue("photo", secondId);
        incomplete.Parameters.AddWithValue("id", latestRevisionId);
        var missing = await Assert.ThrowsAsync<PostgresException>(() => incomplete.ExecuteNonQueryAsync());
        Assert.Equal("CK_ListingRevisionPhotos_Complete", missing.ConstraintName);
    }

    private static async Task<ListingPhotoResult> UploadAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid vehicle, Guid id, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        using var content = new MemoryStream(bytes);
        return await scope.ServiceProvider.GetRequiredService<ListingPhotoService>()
            .UploadAsync(actor, vehicle, id, "image/jpeg", content, CancellationToken.None);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"staff.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, email, "Password123!", role, branch, "Amir Hakim", "0123456789"), CancellationToken.None);
        return (id, email);
    }
}
