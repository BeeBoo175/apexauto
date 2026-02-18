using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Photos;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Storage;
using Dealership.Infrastructure.Photos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class InspectionPhotoTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task PrivateUploadsRetainReceiptsAndRejectChangedContentsStaleAssignmentsAndDirectSql()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var checkIn = await scope.ServiceProvider.GetRequiredService<IAppointmentCheckInStore>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var vehicleId = checkIn.Receipt!.VehicleId;
        using var activeFactory = fixture.CreateFactory();
        using var photoScope = activeFactory.Services.CreateScope();
        var services = photoScope.ServiceProvider;
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var replacement = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var administrator = await ProvisionAsync(services, null, AccountRoles.Administrator);
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var outsider = await ProvisionAsync(services, otherBranch, AccountRoles.WorkshopTechnician);
        var claim = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(technician.Id, vehicleId,
            new(Guid.NewGuid(), technician.Id, 0, null), CancellationToken.None);
        var inspectionId = claim.Receipt!.InspectionId;
        var id = Guid.NewGuid();
        var path = $"/api/staff/inspection/drafts/{inspectionId}/photos/{id}";
        var bytes = IntakePhotoFixture.Image();
        using var client = activeFactory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(client, path, bytes)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), technician.Email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await UploadAsync(client, path, bytes);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await activeFactory.Services.GetRequiredService<PhotoCleanupSignal>().WaitAsync(CancellationToken.None)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var receipt = (await response.Content.ReadFromJsonAsync<InspectionPhotoReceipt>())!;
        Assert.Equal(inspectionId, receipt.InspectionId);
        Assert.Equal(technician.Id, receipt.ActorId);
        Assert.Equal("EP-004", receipt.PointCode);
        var competing = await Task.WhenAll(UploadAsync(client, path, bytes), UploadAsync(client, path, bytes));
        foreach (var replay in competing)
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<InspectionPhotoReceipt>());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(client, path, bytes, point: "EP-005")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(client, path, IntakePhotoFixture.Image(color: SkiaSharp.SKColors.Red))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(client, path, bytes, version: 2)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(client, path, bytes, point: "EP-046")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(client, path, "not an image"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await UploadAsync(client, path, new byte[PhotoContent.MaximumBytes + 1])).StatusCode);
        Assert.Equal(bytes, await client.GetByteArrayAsync(path));

        foreach (var reader in new[] { technician.Id, manager.Id, group.Id })
        {
            using var output = new MemoryStream();
            var read = await services.GetRequiredService<InspectionPhotoService>()
                .DownloadAsync(reader, inspectionId, id, output, CancellationToken.None);
            Assert.Equal(InspectionPhotoStatus.Available, read.Status);
            Assert.Equal(receipt, read.Receipt);
            Assert.Equal(bytes, output.ToArray());
        }
        foreach (var denied in new[] { manager.Id, group.Id, administrator.Id, data.StaffId, data.OwnerId })
        {
            using var input = new MemoryStream(bytes);
            Assert.Equal(InspectionPhotoStatus.Forbidden, (await services.GetRequiredService<InspectionPhotoService>()
                .UploadAsync(denied, inspectionId, Guid.NewGuid(), 1, "EP-004", "image/jpeg", input, CancellationToken.None)).Status);
        }
        Assert.Equal(InspectionPhotoStatus.NotFound, (await services.GetRequiredService<InspectionPhotoService>()
            .GetReceiptAsync(replacement.Id, inspectionId, id, CancellationToken.None)).Status);
        Assert.Equal(InspectionPhotoStatus.NotFound, (await services.GetRequiredService<InspectionPhotoService>()
            .CheckUploadAsync(outsider.Id, inspectionId, 1, CancellationToken.None)));
        Assert.Equal(InspectionPhotoStatus.NotFound, (await services.GetRequiredService<InspectionPhotoService>()
            .GetReceiptAsync(outsider.Id, inspectionId, id, CancellationToken.None)).Status);
        await InspectionPhotoSqlAssertions.VerifyAsync(fixture.Database, id, technician.Id, replacement.Id, manager.Id);
        await InspectionPhotoLifecycleAssertions.VerifyAsync(fixture, activeFactory, client, technician.Id, inspectionId, id, bytes);
        await AssertLostAcknowledgementAsync(client.DefaultRequestHeaders.Authorization!, inspectionId, bytes);
        await AssertExpiryAsync(client.DefaultRequestHeaders.Authorization!, inspectionId, bytes);
        await AssertRevocationDuringReadAsync(client.DefaultRequestHeaders.Authorization!, technician.Id, path, receipt);

        var changed = (byte[])bytes.Clone();
        changed[^1] ^= 1;
        var storageSettings = services.GetRequiredService<PhotoStorageSettings>();
        Assert.Equal(fixture.BucketName, storageSettings.BucketName);
        using var corrupted = new MemoryStream(changed);
        await services.GetRequiredService<IAmazonS3>().PutObjectAsync(new PutObjectRequest
        {
            BucketName = fixture.BucketName,
            Key = $"inspection/photos/{id:N}",
            InputStream = corrupted,
            ContentType = "image/jpeg",
            UseChunkEncoding = false
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(receipt, await client.GetFromJsonAsync<InspectionPhotoReceipt>(path + "/receipt"));

        var pendingId = Guid.NewGuid();
        using (var failed = fixture.CreateFactory(failAfterPut: true))
        using (var failedScope = failed.Services.CreateScope())
        using (var input = new MemoryStream(bytes))
        {
            await Assert.ThrowsAsync<PhotoStorageException>(() => failedScope.ServiceProvider.GetRequiredService<InspectionPhotoService>()
                .UploadAsync(technician.Id, inspectionId, pendingId, 1, "EP-004", "image/jpeg", input, CancellationToken.None));
        }

        var handover = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(manager.Id, vehicleId,
            new(Guid.NewGuid(), replacement.Id, 1, "Workshop handover after evidence upload"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, handover.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/receipt")).StatusCode);
        using var restarted = fixture.CreateFactory();
        using var newScope = restarted.Services.CreateScope();
        var photoService = newScope.ServiceProvider.GetRequiredService<InspectionPhotoService>();
        var photoStore = newScope.ServiceProvider.GetRequiredService<IInspectionPhotoStore>();
        Assert.Equal(InspectionPhotoStatus.NotFound,
            (await photoStore.ConfirmAsync(technician.Id, inspectionId, pendingId, CancellationToken.None)).Status);
        Assert.Equal(InspectionPhotoStatus.Conflict,
            (await photoStore.ConfirmAsync(replacement.Id, inspectionId, pendingId, CancellationToken.None)).Status);
        await InspectionPhotoSqlAssertions.VerifyStaleConfirmationAsync(fixture.Database, pendingId);
        Assert.Equal(receipt, (await photoService.GetReceiptAsync(replacement.Id, inspectionId, id, CancellationToken.None)).Receipt);
        using var newInput = new MemoryStream(bytes);
        Assert.Equal(InspectionPhotoStatus.Conflict, (await photoService.UploadAsync(replacement.Id, inspectionId, id,
            2, "EP-004", "image/jpeg", newInput, CancellationToken.None)).Status);
    }

    private async Task AssertLostAcknowledgementAsync(AuthenticationHeaderValue authorization, Guid inspectionId, byte[] bytes)
    {
        var path = $"/api/staff/inspection/drafts/{inspectionId}/photos/{Guid.NewGuid()}";
        InspectionPhotoReceipt pending;
        using (var failed = fixture.CreateFactory(failAfterPut: true))
        using (var client = failed.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = authorization;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await UploadAsync(client, path, bytes)).StatusCode);
            var response = await client.GetAsync(path + "/receipt");
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            pending = (await response.Content.ReadFromJsonAsync<InspectionPhotoReceipt>())!;
            Assert.Null(pending.AvailableAt);
        }

        using var restarted = fixture.CreateFactory();
        using var retry = restarted.CreateClient();
        retry.DefaultRequestHeaders.Authorization = authorization;
        var resumed = await UploadAsync(retry, path, bytes);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var receipt = (await resumed.Content.ReadFromJsonAsync<InspectionPhotoReceipt>())!;
        Assert.Equal(pending, receipt with { AvailableAt = null });
        Assert.Equal(bytes, await retry.GetByteArrayAsync(path));
    }

    private async Task AssertExpiryAsync(AuthenticationHeaderValue authorization, Guid inspectionId, byte[] bytes)
    {
        using var shortLived = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(2).TotalHours);
        using var client = shortLived.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        var path = $"/api/staff/inspection/drafts/{inspectionId}/photos/{Guid.NewGuid()}";
        var uploaded = await UploadAsync(client, path, bytes);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var receipt = (await uploaded.Content.ReadFromJsonAsync<InspectionPhotoReceipt>())!;
        var remaining = receipt.ExpiresAt - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await UploadAsync(client, path, bytes)).StatusCode);
        Assert.Equal(receipt, await (await client.GetAsync(path + "/receipt")).Content.ReadFromJsonAsync<InspectionPhotoReceipt>());
    }

    private async Task AssertRevocationDuringReadAsync(AuthenticationHeaderValue authorization, Guid technician,
        string path, InspectionPhotoReceipt receipt)
    {
        using var stopped = fixture.CreateFactory(observeDownload: _ =>
        {
            using var services = fixture.Database.CreateServices();
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users.Where(user => user.Id == technician)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false)).GetAwaiter().GetResult();
        });
        using var client = stopped.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        }
        finally
        {
            using var services = fixture.Database.CreateServices();
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DealershipDbContext>().Users.Where(user => user.Id == technician)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        }
        Assert.Equal(receipt, await client.GetFromJsonAsync<InspectionPhotoReceipt>(path + "/receipt"));
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, byte[] bytes,
        string point = "EP-004", long version = 1)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return await client.PutAsync($"{path}?pointCode={point}&assignmentVersion={version}", content);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"danial.hakim.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new StaffProvisioningRequest(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }
}
