using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class InspectionSignoffTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task SigningSealsLatestAttestedEvidenceAndRetainsScopedReportsAcrossRacesAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var (technician, email) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (otherTechnician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var (manager, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var (outsideManager, _) = await ProvisionAsync(services, otherBranch, AccountRoles.BranchManager);
        var claim = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(technician,
            checkIn.Receipt!.VehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = claim.Receipt!.InspectionId;
        var photoId = Guid.NewGuid();
        var image = IntakePhotoFixture.Image();
        using var input = new MemoryStream(image);
        var uploaded = await services.GetRequiredService<InspectionPhotoService>().UploadAsync(technician, inspection,
            photoId, 1, "EP-004", "image/jpeg", input, CancellationToken.None);
        Assert.Equal(InspectionPhotoStatus.Available, uploaded.Status);
        var uploadedPhoto = await services.GetRequiredService<DealershipDbContext>().InspectionPhotos.AsNoTracking()
            .SingleAsync(photo => photo.Id == photoId);
        var pending = await services.GetRequiredService<IInspectionPhotoStore>().PrepareAsync(technician, inspection,
            Guid.NewGuid(), 1, "EP-005", new PhotoContent(uploadedPhoto.ContentType, uploadedPhoto.Bytes,
                uploadedPhoto.Sha256, uploadedPhoto.Width, uploadedPhoto.Height), TimeSpan.FromDays(1), CancellationToken.None);
        Assert.Equal(InspectionPhotoStatus.Pending, pending.Status);
        var saveRequest = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 500m, "Bumper scratch")])
        {
            PhotoIds = [photoId]
        };
        var saves = services.GetRequiredService<IInspectionDraftSaveStore>();
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await saves.SaveAsync(technician, inspection, saveRequest, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await SignAsync(data.Factory, technician, inspection, new(Guid.NewGuid(), 1, 1, 1))).Status);
        saveRequest = saveRequest with
        {
            OperationId = Guid.NewGuid(),
            ExpectedVersion = 1,
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        };
        var saved = await saves.SaveAsync(technician, inspection, saveRequest, CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        foreach (var actor in new[] { technician, manager, group })
        {
            var editor = (await services.GetRequiredService<IInspectionAssignmentStore>()
                .ReadAsync(actor, inspection, CancellationToken.None)).Draft!;
            Assert.Equal(new InspectionPhotoReference(photoId, "EP-004"), Assert.Single(editor.PhotoReferences));
            Assert.Equal(saved.Receipt!.PhotoIds, editor.PhotoReferences.Select(photo => photo.Id));
        }
        var request = new InspectionSignRequest(Guid.NewGuid(), 1, 2, 1);
        foreach (var denied in new[] { manager, group, data.StaffId, data.OwnerId })
        {
            Assert.Equal(InspectionAssignmentStatus.Forbidden, (await SignAsync(data.Factory, denied, inspection, request)).Status);
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound, (await SignAsync(data.Factory, otherTechnician, inspection, request)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await SignAsync(data.Factory, technician, inspection, request with { SaveVersion = 1 })).Status);

        await InspectionSignoffSqlAssertions.VerifyUnsignedAttacksAsync(fixture.Database, inspection, request, saved.Receipt!.OperationId);
        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var oldSnapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"IsSigned\" FROM \"InspectionDrafts\" WHERE \"Id\" = @inspection", stale, oldSnapshot))
        {
            read.Parameters.AddWithValue("inspection", inspection);
            Assert.Equal(false, await read.ExecuteScalarAsync());
        }

        var race = await Task.WhenAll(SignAsync(data.Factory, technician, inspection, request),
            SignAsync(data.Factory, technician, inspection, request with { OperationId = Guid.NewGuid() }));
        var report = Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Succeeded).Report!;
        Assert.Single(race, result => result.Status == InspectionAssignmentStatus.Conflict);
        request = request with { OperationId = report.OperationId };
        await InspectionSignoffSqlAssertions.VerifyOldSnapshotAsync(stale, oldSnapshot, inspection);
        await oldSnapshot.RollbackAsync();
        await InspectionSignoffSqlAssertions.VerifySignedAttacksAsync(fixture.Database, report);

        using var restarted = fixture.CreateFactory();
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize((await SignAsync(restarted, technician, inspection, request)).Report));
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await SignAsync(restarted, technician, inspection, request with { SaveVersion = 1 })).Status);
        using var restartedScope = restarted.Services.CreateScope();
        var restored = restartedScope.ServiceProvider;
        foreach (var actor in new[] { technician, manager, group })
        {
            var read = await restored.GetRequiredService<IInspectionReportStore>().ReadAsync(actor, inspection, 1, CancellationToken.None);
            Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(read.Report));
        }
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await restored.GetRequiredService<IInspectionReportStore>().ReadAsync(otherTechnician, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await restored.GetRequiredService<IInspectionReportStore>().ReadAsync(outsideManager, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await restored.GetRequiredService<IInspectionReportStore>().ReadAsync(data.OwnerId, inspection, 1, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await restored.GetRequiredService<IInspectionDraftSaveStore>()
            .SaveAsync(technician, inspection, saveRequest with { OperationId = Guid.NewGuid(), ExpectedVersion = 2 }, CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await restored.GetRequiredService<IInspectionAssignmentStore>()
            .AssignAsync(manager, report.Vehicle.Id, new(Guid.NewGuid(), otherTechnician, 1, "Workshop handover"), CancellationToken.None)).Status);
        Assert.Equal(InspectionPhotoStatus.Conflict, await restored.GetRequiredService<InspectionPhotoService>()
            .CheckUploadAsync(technician, inspection, 1, CancellationToken.None));
        using var output = new MemoryStream();
        Assert.Equal(InspectionPhotoStatus.Available, (await restored.GetRequiredService<InspectionPhotoService>()
            .DownloadAsync(technician, inspection, photoId, output, CancellationToken.None)).Status);
        Assert.Equal(image, output.ToArray());
        var draft = await restored.GetRequiredService<IInspectionAssignmentStore>().ReadAsync(technician, inspection, CancellationToken.None);
        Assert.True(draft.Draft!.IsSigned);
        Assert.Equal(1, draft.Draft.Revision);
        Assert.Equal(new InspectionPhotoReference(photoId, "EP-004"), Assert.Single(draft.Draft.PhotoReferences));
        await VerifyApiAsync(restarted, email, inspection, request, report);
        var database = restored.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == technician).ExecuteUpdateAsync(update =>
            update.SetProperty(user => user.UserName, "changed.identity@example.invalid"));
        var historical = await restored.GetRequiredService<IInspectionReportStore>().ReadAsync(group, inspection, 1, CancellationToken.None);
        Assert.Equal(email, historical.Report!.TechnicianIdentity);
        await database.Users.Where(user => user.Id == technician).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(InspectionAssignmentStatus.Forbidden, (await SignAsync(restarted, technician, inspection, request)).Status);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email, Guid inspection,
        InspectionSignRequest request, InspectionReport report)
    {
        using var client = factory.CreateClient();
        var signPath = $"/api/staff/inspection/drafts/{inspection}/sign";
        var reportPath = $"/api/staff/inspection/reports/{inspection}/revisions/1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(reportPath)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var replay = await client.PostAsJsonAsync(signPath, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.CacheControl?.NoStore);
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(await replay.Content.ReadFromJsonAsync<InspectionReport>()));
        var read = await client.GetAsync(reportPath);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(signPath, request with { SaveVersion = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(signPath, request with { Revision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(reportPath.Replace("/1", "/2", StringComparison.Ordinal))).StatusCode);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"azlan.hakimi.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<InspectionReportResult> SignAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid inspection, InspectionSignRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionReportStore>()
            .SignAsync(actor, inspection, request, CancellationToken.None);
    }
}
