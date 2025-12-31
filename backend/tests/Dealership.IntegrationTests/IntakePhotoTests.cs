using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Dealership.IntegrationTests;

public sealed class IntakePhotoTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task DownloadDeadlineAbortsStalledResponsesAndReleasesFilesAndTransferPermits()
    {
        var probe = new PhotoDownloadProbe();
        using var factory = fixture.CreateFactory(clock: probe.Clock, observeDownload: probe.Files.Enqueue)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(probe)));
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        using var requests = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);
        var id = Guid.NewGuid();
        var bytes = IntakePhotoFixture.Image();
        Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(client, id, bytes)).StatusCode);
        var downloads = Enumerable.Range(0, 4).Select(_ => client.GetAsync($"/api/intake/photos/{id}",
            HttpCompletionOption.ResponseHeadersRead, requests.Token)).ToArray();

        try
        {
            await probe.AllStalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync($"/api/intake/photos/{id}")).StatusCode);
            var files = probe.Files.Cast<FileStream>().ToArray();
            Assert.Equal(4, files.Length);
            Assert.All(files, file => Assert.True(file.CanRead));

            probe.ExpireDeadlines();
            await probe.AllCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.All(files, file => Assert.False(file.CanRead));
            Assert.All(files, file => Assert.False(File.Exists(file.Name)));
            var responses = await Task.WhenAll(downloads);

            foreach (var response in responses)
            {
                using (response)
                {
                    var failure = await Assert.ThrowsAsync<HttpRequestException>(() => response.Content.ReadAsByteArrayAsync());
                    Assert.IsType<IOException>(failure.InnerException);
                }
            }

            probe.Release.TrySetResult();
            Assert.Equal(bytes, await client.GetByteArrayAsync($"/api/intake/photos/{id}"));
        }
        finally
        {
            probe.Release.TrySetResult();
            await requests.CancelAsync();
            await Task.WhenAll(downloads.Select(async download =>
            {
                try
                {
                    (await download).Dispose();
                }
                catch (OperationCanceledException)
                {
                }
            }));
        }
    }

    [Fact]
    public async Task VerifiedOwnerUploadsRealFormatsAndContentOwnershipAndTransportAreEnforced()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        var bytes = IntakePhotoFixture.Image();
        Assert.Equal(HttpStatusCode.Unauthorized, (await IntakePhotoFixture.UploadAsync(client, id, bytes)).StatusCode);
        var registration = await IntakePhotoFixture.RegisterAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await IntakePhotoFixture.UploadAsync(client, id, bytes)).StatusCode);
        await IntakePhotoFixture.VerifyAsync(factory, client, registration);

        var first = await IntakePhotoFixture.UploadAsync(client, id, bytes);
        Assert.True(first.StatusCode == HttpStatusCode.OK, $"Upload returned {first.StatusCode}: {fixture.LastStorageFailure}");
        Assert.True(first.Headers.CacheControl?.NoStore);
        var receipt = (await first.Content.ReadFromJsonAsync<PhotoReceipt>())!;
        Assert.Equal("Driver", receipt.Angle);
        Assert.Equal(TimeSpan.FromHours(24), receipt.ExpiresAt - receipt.CreatedAt);
        Assert.NotNull(receipt.AvailableAt);
        Assert.Equal(receipt, await client.GetFromJsonAsync<PhotoReceipt>($"/api/intake/photos/{id}/receipt"));
        Assert.Equal(bytes, await client.GetByteArrayAsync($"/api/intake/photos/{id}"));
        Assert.Equal(receipt, await (await IntakePhotoFixture.UploadAsync(client, id, bytes)).Content.ReadFromJsonAsync<PhotoReceipt>());
        Assert.Equal(HttpStatusCode.Conflict, (await IntakePhotoFixture.UploadAsync(client, id, bytes, "Rear")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntakePhotoFixture.UploadAsync(client, id,
            IntakePhotoFixture.Image(color: SKColors.Blue))).StatusCode);

        foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Webp })
        {
            var mime = format == SKEncodedImageFormat.Png ? "image/png" : "image/webp";
            Assert.Equal(HttpStatusCode.OK, (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(),
                IntakePhotoFixture.Image(format), "Front", mime, unknownLength: true)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), "not a photo"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), bytes, mime: "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), bytes, mime: "application/octet-stream")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), bytes, "1")).StatusCode);

        var maximum = new byte[Dealership.Domain.Intake.IntakePhoto.MaximumBytes];
        bytes.CopyTo(maximum, 0);
        Assert.Equal(HttpStatusCode.OK,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), maximum)).StatusCode);
        var oversized = new byte[maximum.Length + 1];
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), oversized)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), oversized, unknownLength: true)).StatusCode);

        using var otherClient = factory.CreateClient();
        var other = await IntakePhotoFixture.RegisterAsync(otherClient);
        await IntakePhotoFixture.VerifyAsync(factory, otherClient, other);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/intake/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/intake/photos/{id}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntakePhotoFixture.UploadAsync(otherClient, id, bytes)).StatusCode);

        await fixture.CorruptOwnedObjectAsync(id, bytes);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync($"/api/intake/photos/{id}")).StatusCode);
        Assert.Equal(receipt, await client.GetFromJsonAsync<PhotoReceipt>($"/api/intake/photos/{id}/receipt"));

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var photo = await database.IntakePhotos.AsNoTracking().SingleAsync(item => item.Id == id);
        await database.Users.Where(user => user.Id == photo.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/intake/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await IntakePhotoFixture.UploadAsync(client, id, bytes)).StatusCode);
        Assert.Equal(receipt.AvailableAt, photo.AvailableAt);
        await database.Users.Where(user => user.Id == photo.OwnerId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.EmailConfirmed, true).SetProperty(user => user.Role, AccountRoles.Administrator));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/intake/photos/{id}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await IntakePhotoFixture.UploadAsync(client, Guid.NewGuid(), bytes)).StatusCode);
    }

    [Fact]
    public async Task LostStorageAcknowledgementReconcilesAcrossRestartAndCompetingRetriesKeepFirstReceipt()
    {
        var id = Guid.NewGuid();
        var bytes = IntakePhotoFixture.Image();
        RegistrationRequest registration;

        using (var failed = fixture.CreateFactory(failAfterPut: true))
        using (var client = failed.CreateClient())
        {
            registration = await IntakePhotoFixture.RegisterAsync(client);
            await IntakePhotoFixture.VerifyAsync(failed, client, registration);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await IntakePhotoFixture.UploadAsync(client, id, bytes)).StatusCode);
            var pending = await client.GetAsync($"/api/intake/photos/{id}/receipt");
            Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
            Assert.Null((await pending.Content.ReadFromJsonAsync<PhotoReceipt>())!.AvailableAt);
            Assert.Equal(HttpStatusCode.Accepted, (await client.GetAsync($"/api/intake/photos/{id}")).StatusCode);
        }

        using var restarted = fixture.CreateFactory();
        using var restartedClient = restarted.CreateClient();
        await IntakePhotoFixture.LoginAsync(restartedClient, registration);
        var competing = await Task.WhenAll(IntakePhotoFixture.UploadAsync(restartedClient, id, bytes),
            IntakePhotoFixture.UploadAsync(restartedClient, id, bytes));
        Assert.All(competing, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var first = await competing[0].Content.ReadFromJsonAsync<PhotoReceipt>();
        var second = await competing[1].Content.ReadFromJsonAsync<PhotoReceipt>();
        Assert.Equal(first, second);
        Assert.Equal(bytes, await restartedClient.GetByteArrayAsync($"/api/intake/photos/{id}"));

        var disputedId = Guid.NewGuid();
        var blue = IntakePhotoFixture.Image(color: SKColors.Blue);
        var disputed = await Task.WhenAll(IntakePhotoFixture.UploadAsync(restartedClient, disputedId, bytes),
            IntakePhotoFixture.UploadAsync(restartedClient, disputedId, blue, "Rear"));
        Assert.Single(disputed, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(disputed, response => response.StatusCode == HttpStatusCode.Conflict);
        var winner = disputed[0].StatusCode == HttpStatusCode.OK ? bytes : blue;
        Assert.Equal(winner, await restartedClient.GetByteArrayAsync($"/api/intake/photos/{disputedId}"));

        var now = first!.ExpiresAt;
        using var expired = fixture.CreateFactory(clock: new PhotoClock(now));
        using var scope = expired.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IntakePhotoService>();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var ownerId = await database.IntakePhotos.Where(photo => photo.Id == id).Select(photo => photo.OwnerId).SingleAsync();
        Assert.Equal(PhotoStatus.NotFound, (await service.GetReceiptAsync(ownerId, id, CancellationToken.None)).Status);
        using var content = new MemoryStream(bytes);
        Assert.Equal(PhotoStatus.NotFound,
            (await service.UploadAsync(ownerId, id, Dealership.Domain.Intake.PhotoAngle.Driver, "image/jpeg", content, CancellationToken.None)).Status);
    }

    private sealed class PhotoClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
