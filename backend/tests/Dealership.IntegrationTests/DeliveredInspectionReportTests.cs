using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class DeliveredInspectionReportTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OnlyDeliveredBuyerReadsSignedReportsAndTheirEvidenceAcrossRestartAndEligibilityChanges()
    {
        var world = await SaleWorld.SharedAsync(fixture);
        var hold = await world.HoldAsync(withInspectionEvidence: true);
        await using var database = NewContext();
        var draft = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.VehicleId == hold.VehicleId);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.InspectionId == draft.Id && item.Revision == 1);
        var report = signed.Report();
        var evidence = Assert.Single(report.Checklist.SelectMany(point => point.Photos)).Id;
        var privatePhotos = await database.InspectionPhotos.Where(item => item.InspectionId == draft.Id && item.Id != evidence)
            .Select(item => item.Id).ToArrayAsync();
        Assert.Equal(2, privatePhotos.Length);
        var reports = world.Services.GetRequiredService<IInspectionReportStore>();
        var photos = world.Services.GetRequiredService<IInspectionPhotoStore>();
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 1, default)).Status);
        Assert.Equal(InspectionPhotoStatus.NotFound,
            (await photos.FindAsync(hold.CustomerId, draft.Id, evidence, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded,
            (await world.Handling.ClaimAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 1), default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded,
            (await world.Terms.SubmitAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 2), default)).Status);
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == hold.Id);
        var profile = await database.CustomerProfiles.AsNoTracking().SingleAsync(item => item.UserId == hold.CustomerId);
        var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == hold.VehicleId);
        var review = new SaleSettlementReview(profile.LegalName, profile.Nric, profile.ResidentialAddress,
            SaleReviewedVehicle.Of(vehicle));
        var payment = new SalePaymentEvidence(SalePaymentMethod.Cash, sale.BalancePayable, "CASH 53819",
            new DateOnly(2026, 11, 2), null, 0m, null, false, false);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Settlement.SettleAsync(world.Agent, hold.Id,
            new(Guid.NewGuid(), 3, payment, review), default)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 1, default)).Status);
        Assert.Equal(SaleResultStatus.Succeeded, (await world.Services.GetRequiredService<SaleHandoverService>()
            .DeliverAsync(world.Agent, hold.Id, new(Guid.NewGuid(), 4,
                new(vehicle.MileageKilometres + 100, 2, true, true, true)), default)).Status);

        using var restarted = fixture.CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var restored = scope.ServiceProvider;
        reports = restored.GetRequiredService<IInspectionReportStore>();
        photos = restored.GetRequiredService<IInspectionPhotoStore>();
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 1, default)).Report));
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 2, default)).Status);
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 3, default)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await reports.ReadAsync(world.Manager, draft.Id, 1, default)).Status);
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await reports.ReadAsync(world.Agent, draft.Id, 1, default)).Status);
        Assert.NotEqual(hold.CustomerId, report.Vehicle.IntakeOwnerId);
        Assert.Equal(InspectionAssignmentStatus.NotFound,
            (await reports.ReadAsync(report.Vehicle.IntakeOwnerId, draft.Id, 1, default)).Status);
        foreach (var id in privatePhotos.Append(Guid.NewGuid()))
        {
            Assert.Equal(InspectionPhotoStatus.NotFound, (await photos.FindAsync(hold.CustomerId, draft.Id, id, default)).Status);
        }

        using var client = restarted.CreateClient();
        var reportPath = $"/api/staff/inspection/reports/{draft.Id}/revisions/1/document";
        var photoPath = $"/api/staff/inspection/drafts/{draft.Id}/photos/{evidence}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(reportPath)).StatusCode);
        var email = await database.Users.Where(item => item.Id == hold.CustomerId).Select(item => item.Email).SingleAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.GetAsync(reportPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..4]), StringComparison.Ordinal);
        var downloaded = await client.GetAsync(photoPath);
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.True(downloaded.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(downloaded.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(IntakePhotoFixture.Image(), await downloaded.Content.ReadAsByteArrayAsync());
        foreach (var id in privatePhotos)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(photoPath.Replace(evidence.ToString(), id.ToString()))).StatusCode);
        }

        foreach (var suffix in new[] { "", "/workspace" })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync($"/api/staff/inspection/drafts/{draft.Id}{suffix}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/staff/inspection/reports/{draft.Id}/revisions")).StatusCode);
        Assert.Equal(InspectionPhotoStatus.Forbidden, await restored.GetRequiredService<InspectionPhotoService>()
            .CheckUploadAsync(hold.CustomerId, draft.Id, draft.AssignmentVersion, default));

        await database.Users.Where(item => item.Id == hold.CustomerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 1, default)).Status);
        using var changedFactory = fixture.CreateFactory(observeDownload: _ =>
        {
            using var changed = NewContext();
            changed.Users.Where(item => item.Id == hold.CustomerId)
                .ExecuteUpdate(update => update.SetProperty(item => item.IsActive, false));
        });
        using var changedClient = changedFactory.CreateClient();
        changedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var deniedDownload = await changedClient.GetAsync(photoPath);
        Assert.Equal(HttpStatusCode.Forbidden, deniedDownload.StatusCode);
        Assert.NotEqual("image/jpeg", deniedDownload.Content.Headers.ContentType?.MediaType);
        Assert.Equal(InspectionAssignmentStatus.Forbidden,
            (await reports.ReadAsync(hold.CustomerId, draft.Id, 1, default)).Status);
        Assert.Equal(InspectionPhotoStatus.Forbidden,
            (await photos.FindAsync(hold.CustomerId, draft.Id, evidence, default)).Status);
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
