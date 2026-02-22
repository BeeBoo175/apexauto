using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class InspectionPhotoLifecycleAssertions
{
    public static async Task VerifyAsync(IntakePhotoFixture fixture, WebApplicationFactory<Program> factory,
        HttpClient client, Guid actor, Guid inspection, Guid originalPhoto, byte[] bytes)
    {
        var ids = new List<Guid> { originalPhoto };
        for (var index = 0; index < 4; index++)
        {
            ids.Add((await UploadAsync(factory, actor, inspection, Guid.NewGuid(), bytes)).Receipt!.Id);
        }

        var request = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 0,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 250m, "Bumper scratch with retained images")])
        {
            PhotoIds = [.. ids.Take(4).Reverse()]
        };
        foreach (var invalid in new[]
        {
            request with { PhotoIds = null }, request with { PhotoIds = [Guid.Empty] },
            request with { PhotoIds = [Guid.NewGuid()] }, request with { PhotoIds = [originalPhoto, originalPhoto] },
            request with { PhotoIds = ids }, request with { Entries = [] },
            request with { Entries = [new("IE-040", InspectionEntryKind.NotFitted, null, 0, "No powered roof fitted")] }
        })
        {
            Assert.Equal(InspectionAssignmentStatus.Invalid, (await SaveAsync(factory, actor, inspection, invalid)).Status);
        }

        var saved = await SaveAsync(factory, actor, inspection, request);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        Assert.Equal(ids.Take(4).Order(), saved.Receipt!.PhotoIds);
        var endpoint = $"/api/staff/inspection/drafts/{inspection}/saves";
        var apiReplay = await client.PostAsJsonAsync(endpoint, request);
        Assert.Equal(HttpStatusCode.OK, apiReplay.StatusCode);
        Assert.True(apiReplay.Headers.CacheControl?.NoStore);
        Assert.Equal(JsonSerializer.Serialize(saved.Receipt),
            JsonSerializer.Serialize(await apiReplay.Content.ReadFromJsonAsync<InspectionDraftSaveReceipt>()));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(endpoint, request with { PhotoIds = [] })).StatusCode);
        var read = await client.GetFromJsonAsync<InspectionDraftSnapshot>($"/api/staff/inspection/drafts/{inspection}");
        Assert.Equal(saved.Receipt.PhotoIds, read!.LatestSave!.PhotoIds);
        Assert.Equal(saved.Receipt.PhotoIds, read.PhotoReferences.Select(photo => photo.Id));
        Assert.All(read.PhotoReferences, photo => Assert.Equal("EP-004", photo.PointCode));
        using var restarted = fixture.CreateFactory();
        var replay = await SaveAsync(restarted, actor, inspection, request with { PhotoIds = [.. request.PhotoIds!.Reverse()] });
        Assert.Equal(JsonSerializer.Serialize(saved.Receipt), JsonSerializer.Serialize(replay.Receipt));
        Assert.Equal(InspectionAssignmentStatus.Conflict,
            (await SaveAsync(restarted, actor, inspection, request with { PhotoIds = [] })).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await SaveAsync(restarted, actor, inspection, new(Guid.NewGuid(), 1, 1, []))).Status);
        using (var readScope = restarted.Services.CreateScope())
        {
            var cleared = (await readScope.ServiceProvider.GetRequiredService<IInspectionAssignmentStore>()
                .ReadAsync(actor, inspection, CancellationToken.None)).Draft!;
            Assert.Empty(cleared.PhotoReferences);
        }
        Assert.Equal(JsonSerializer.Serialize(saved.Receipt),
            JsonSerializer.Serialize((await SaveAsync(restarted, actor, inspection, request)).Receipt));
        await InspectionPhotoAttachmentSqlAssertions.VerifyAsync(fixture.Database, saved.Receipt, ids[4]);
        await VerifyCleanupAsync(fixture, actor, inspection, bytes);
    }

    private static async Task VerifyCleanupAsync(IntakePhotoFixture fixture, Guid actor, Guid inspection, byte[] bytes)
    {
        using var shortLived = fixture.CreateFactory(unsubmittedLifetimeHours: TimeSpan.FromSeconds(4).TotalHours);
        var retained = await UploadAsync(shortLived, actor, inspection, Guid.NewGuid(), bytes);
        var detached = await UploadAsync(shortLived, actor, inspection, Guid.NewGuid(), bytes);
        Assert.NotNull(detached.Receipt);
        var request = new InspectionDraftSaveRequest(Guid.NewGuid(), 1, 2,
            [new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.Advisory, 0, "Evidence retained for examination")])
        {
            PhotoIds = [retained.Receipt!.Id]
        };
        var pendingId = Guid.NewGuid();
        using (var uncertain = fixture.CreateFactory(failAfterPut: true,
            unsubmittedLifetimeHours: TimeSpan.FromSeconds(2).TotalHours))
        {
            await Assert.ThrowsAsync<PhotoStorageException>(() => UploadAsync(uncertain, actor, inspection, pendingId, bytes));
        }

        await InspectionPhotoAttachmentSqlAssertions.VerifyStaleRemovalAsync(fixture.Database, retained.Receipt.Id, async () =>
        {
            Assert.Equal(InspectionAssignmentStatus.Succeeded, (await SaveAsync(shortLived, actor, inspection, request)).Status);
            var remaining = detached.Receipt!.ExpiresAt - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }
        });
        using var restarted = fixture.CreateFactory();
        using var scope = restarted.Services.CreateScope();
        var services = scope.ServiceProvider;
        var photos = services.GetRequiredService<InspectionPhotoService>();
        using var output = new MemoryStream();
        Assert.Equal(InspectionPhotoStatus.Available,
            (await photos.DownloadAsync(actor, inspection, retained.Receipt.Id, output, CancellationToken.None)).Status);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await SaveAsync(restarted, actor, inspection,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = 3, PhotoIds = [detached.Receipt.Id] })).Status);
        await InspectionPhotoAttachmentSqlAssertions.VerifyRemovalDenialsAsync(fixture.Database,
            retained.Receipt.Id, detached.Receipt.Id);

        await using (var transferScope = restarted.Services.CreateAsyncScope())
        {
            await using var transfer = await transferScope.ServiceProvider.GetRequiredService<IPhotoTransferLock>()
                .AcquireAsync(detached.Receipt.Id, CancellationToken.None);
            await RunCleanupAsync(restarted);
            Assert.Equal(InspectionPhotoStatus.Gone,
                (await photos.GetReceiptAsync(actor, inspection, detached.Receipt.Id, CancellationToken.None)).Status);
        }

        using (var failed = fixture.CreateFactory(failBeforeDelete: true))
        {
            await InspectionPhotoAttachmentSqlAssertions.VerifyStaleAttachmentAsync(fixture.Database,
                inspection, detached.Receipt.Id, () => RunCleanupAsync(failed));
        }
        Assert.Equal(InspectionPhotoStatus.DeletionPending,
            (await photos.GetReceiptAsync(actor, inspection, detached.Receipt.Id, CancellationToken.None)).Status);
        var database = services.GetRequiredService<DealershipDbContext>();
        var oldClaim = (await database.InspectionPhotoRemovals.AsNoTracking()
            .SingleAsync(item => item.PhotoId == detached.Receipt.Id)).ClaimId!.Value;
        await ExpireClaimsAsync(database);
        using (var lostAck = fixture.CreateFactory(failAfterDelete: true))
        {
            await RunCleanupAsync(lostAck);
        }
        Assert.Equal(InspectionPhotoStatus.DeletionPending,
            (await photos.GetReceiptAsync(actor, inspection, detached.Receipt.Id, CancellationToken.None)).Status);
        await ExpireClaimsAsync(database);
        var store = services.GetRequiredService<InspectionPhotoCleanupStore>();
        await using (var transfer = await services.GetRequiredService<IPhotoTransferLock>()
            .AcquireAsync(detached.Receipt.Id, CancellationToken.None))
        {
            var claim = (await store.ClaimAsync(detached.Receipt.Id, CancellationToken.None))!;
            Assert.NotEqual(oldClaim, claim.ClaimId);
            await store.MarkDeletedAsync(detached.Receipt.Id, oldClaim, CancellationToken.None);
            Assert.Null((await database.InspectionPhotoRemovals.AsNoTracking()
                .SingleAsync(item => item.PhotoId == detached.Receipt.Id)).DeletedAt);
            await services.GetRequiredService<IPhotoStorage>().DeleteAsync(claim.Photo, CancellationToken.None);
            await store.MarkDeletedAsync(detached.Receipt.Id, claim.ClaimId, CancellationToken.None);
        }
        await RunCleanupAsync(restarted);
        Assert.Equal(InspectionPhotoStatus.Deleted,
            (await photos.GetReceiptAsync(actor, inspection, detached.Receipt.Id, CancellationToken.None)).Status);
        Assert.Equal(InspectionPhotoStatus.Deleted,
            (await photos.GetReceiptAsync(actor, inspection, pendingId, CancellationToken.None)).Status);
        Assert.Equal(InspectionPhotoStatus.Available,
            (await photos.GetReceiptAsync(actor, inspection, retained.Receipt.Id, CancellationToken.None)).Status);
        foreach (var id in new[] { detached.Receipt.Id, pendingId })
        {
            var removed = await database.InspectionPhotos.AsNoTracking().SingleAsync(photo => photo.Id == id);
            await Assert.ThrowsAsync<PhotoStorageException>(() => services.GetRequiredService<IPhotoStorage>()
                .CopyToAsync(removed, Stream.Null, CancellationToken.None));
        }
        await InspectionPhotoAttachmentSqlAssertions.VerifyRemovalHistoryAsync(fixture.Database, detached.Receipt.Id);
        Assert.Equal(5, await database.InspectionDraftPhotos.CountAsync());
    }

    private static async Task ExpireClaimsAsync(DealershipDbContext database)
    {
        await database.InspectionPhotoRemovals.Where(item => item.DeletedAt == null).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.ClaimExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1))
            .SetProperty(item => item.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    private static async Task RunCleanupAsync(WebApplicationFactory<Program> factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<PhotoCleanupWorker>(factory.Services);
        await worker.RunBatchAsync(CancellationToken.None);
    }

    private static async Task<InspectionPhotoResult> UploadAsync(WebApplicationFactory<Program> factory, Guid actor,
        Guid inspection, Guid photo, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        using var content = new MemoryStream(bytes);
        return await scope.ServiceProvider.GetRequiredService<InspectionPhotoService>().UploadAsync(actor, inspection,
            photo, 1, "EP-004", "image/jpeg", content, CancellationToken.None);
    }

    private static async Task<InspectionDraftSaveResult> SaveAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid inspection, InspectionDraftSaveRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInspectionDraftSaveStore>()
            .SaveAsync(actor, inspection, request, CancellationToken.None);
    }
}
