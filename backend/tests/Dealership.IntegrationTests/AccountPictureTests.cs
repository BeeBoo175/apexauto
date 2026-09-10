using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Dealership.IntegrationTests;

public sealed class AccountPictureTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OwnerUploadsThumbnailStaffViewAndOnlyOwnerOrAdministratorRemoves()
    {
        using var factory = fixture.CreateFactory();
        using var customerClient = factory.CreateClient();
        var customer = await IntakePhotoFixture.RegisterAsync(customerClient);
        var customerId = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        using var otherClient = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(otherClient);

        using var unsupported = await PutAsync(customerClient, Guid.NewGuid(), [1, 2, 3], "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        AssertPictureHeaders(unsupported);
        using var invalid = await PutAsync(customerClient, Guid.NewGuid(), [1, 2, 3], "image/jpeg");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        AssertPictureHeaders(invalid);
        var wide = Image(SKEncodedImageFormat.Png, 600, 300);
        var firstId = Guid.NewGuid();
        var stored = await PutAsync(customerClient, firstId, wide, "image/png");
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        AssertPictureHeaders(stored);
        var first = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId;
        Assert.Equal(firstId, first);

        var burst = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => customerClient.GetAsync($"/api/accounts/{customerId}/picture")));
        foreach (var response in burst)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertPictureHeaders(response);
            }
        }

        var thumbnail = await customerClient.GetAsync($"/api/accounts/{customerId}/picture");
        AssertPictureHeaders(thumbnail);
        Assert.Equal("image/jpeg", thumbnail.Content.Headers.ContentType?.MediaType);
        using var decoded = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync());
        Assert.Equal((256, 256), (decoded.Width, decoded.Height));
        using var forbiddenRead = await otherClient.GetAsync($"/api/accounts/{customerId}/picture");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
        AssertPictureHeaders(forbiddenRead);
        using var forbiddenRemoval = await otherClient.DeleteAsync($"/api/accounts/{customerId}/picture/{firstId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRemoval.StatusCode);
        AssertPictureHeaders(forbiddenRemoval);

        var secondId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(customerClient, secondId, Image(SKEncodedImageFormat.Jpeg, 40, 40), "image/jpeg")).StatusCode);
        var second = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId;
        Assert.Equal(secondId, second);

        using var staffClient = factory.CreateClient();
        await SignInStaffAsync(factory, staffClient, AccountRoles.GeneralManager);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync($"/api/accounts/{customerId}/picture")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.DeleteAsync($"/api/accounts/{customerId}/picture/{secondId}")).StatusCode);

        using var administratorClient = factory.CreateClient();
        await SignInStaffAsync(factory, administratorClient, AccountRoles.Administrator);
        using var removed = await administratorClient.DeleteAsync($"/api/accounts/{customerId}/picture/{secondId}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal("Removed", (await removed.Content.ReadFromJsonAsync<AccountPictureReceipt>())!.RemovalReason);
        AssertPictureHeaders(removed);
        using var missing = await customerClient.GetAsync($"/api/accounts/{customerId}/picture");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        AssertPictureHeaders(missing);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<Dealership.Infrastructure.Persistence.DealershipDbContext>();
            var removal = Assert.Single(database.AccountEvents.Where(item => item.TargetId == customerId));
            Assert.Equal(Dealership.Domain.Accounts.AccountEventKind.PictureRemoved, removal.Kind);
            Assert.NotEqual(customerId, removal.ActorId);
        }

        Assert.Null((await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId);
        // The removal receipt is idempotent for the picture that was seen, and an unknown id is not found.
        Assert.Equal(HttpStatusCode.OK, (await customerClient.DeleteAsync($"/api/accounts/{customerId}/picture/{secondId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customerClient.DeleteAsync($"/api/accounts/{customerId}/picture/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task StaffSeeCustomersByNameAndColleaguesInFullWhileCustomersSeeNobody()
    {
        using var factory = fixture.CreateFactory();
        using var customerClient = factory.CreateClient();
        var customer = await IntakePhotoFixture.RegisterAsync(customerClient);
        var customerId = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        using var managerClient = factory.CreateClient();
        await SignInStaffAsync(factory, managerClient, AccountRoles.GeneralManager);
        var managerId = (await managerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;

        using var response = await managerClient.GetAsync($"/api/people/{customerId}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var asCustomer = (await response.Content.ReadFromJsonAsync<Person>())!;
        Assert.Equal(customer.LegalName, asCustomer.Name);
        Assert.Equal(customer.Username, asCustomer.Username);
        Assert.Null(asCustomer.Email);
        Assert.Null(asCustomer.Mobile);

        using var colleagueClient = factory.CreateClient();
        await SignInStaffAsync(factory, colleagueClient, AccountRoles.Administrator);
        var colleague = (await colleagueClient.GetFromJsonAsync<Person>($"/api/people/{managerId}"))!;
        Assert.Equal("Amir Hakim", colleague.Name);
        Assert.Equal("0123456789", colleague.Mobile);
        Assert.NotNull(colleague.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await customerClient.GetAsync($"/api/people/{managerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await managerClient.GetAsync($"/api/people/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task InterruptedReplacementKeepsThePreviousPictureAndRetrySettlesUnderOnePictureId()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(client);
        using var otherClient = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(otherClient);
        var ownerId = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var replacement = Image(SKEncodedImageFormat.Jpeg, 40, 40);
        Assert.Equal(HttpStatusCode.OK,
            (await PutAsync(client, first, Image(SKEncodedImageFormat.Png, 600, 300), "image/png")).StatusCode);

        // The object reaches storage but the acknowledgement is lost, as in a storage outage after the write.
        using (var outage = fixture.CreateFactory(failAfterPut: true))
        using (var outageClient = outage.CreateClient())
        {
            outageClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            using var lost = await PutAsync(outageClient, second, replacement, "image/jpeg");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, lost.StatusCode);
        }

        Assert.Equal(first, await CurrentPictureAsync(client));
        using (var pending = await client.GetAsync($"/api/accounts/me/picture/{second}/receipt"))
        {
            Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
            Assert.Equal("Pending", (await pending.Content.ReadFromJsonAsync<AccountPictureReceipt>())!.Status);
        }

        using (var download = await client.GetAsync($"/api/accounts/{ownerId}/picture"))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        }

        using var settled = await PutAsync(client, second, replacement, "image/jpeg");
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var receipt = (await settled.Content.ReadFromJsonAsync<AccountPictureReceipt>())!;
        Assert.Equal("Available", receipt.Status);
        Assert.Equal(second, await CurrentPictureAsync(client));
        using var replay = await PutAsync(client, second, replacement, "image/jpeg");
        Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<AccountPictureReceipt>());
        using var differentContents = await PutAsync(client, second, Image(SKEncodedImageFormat.Png, 50, 50, SKColors.OrangeRed), "image/png");
        Assert.Equal(HttpStatusCode.Conflict, differentContents.StatusCode);
        using var anotherOwner = await PutAsync(otherClient, second, replacement, "image/jpeg");
        Assert.Equal(HttpStatusCode.Conflict, anotherOwner.StatusCode);
        using var ownerOnlyReceipt = await otherClient.GetAsync($"/api/accounts/me/picture/{second}/receipt");
        Assert.Equal(HttpStatusCode.NotFound, ownerOnlyReceipt.StatusCode);
    }

    [Fact]
    public async Task RemovalIsBoundToTheSeenPictureAndNeverResolvesToTheCurrentOne()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(client);
        var ownerId = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        var seen = Guid.NewGuid();
        var current = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(client, seen, Image(SKEncodedImageFormat.Png, 60, 60), "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(client, current, Image(SKEncodedImageFormat.Jpeg, 40, 40), "image/jpeg")).StatusCode);

        // The screen still shows the first picture; its removal is answered from its own receipt.
        using var stale = await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{seen}");
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var staleReceipt = (await stale.Content.ReadFromJsonAsync<AccountPictureReceipt>())!;
        Assert.Equal(seen, staleReceipt.PictureId);
        Assert.Equal("Replaced", staleReceipt.RemovalReason);
        Assert.Equal(current, await CurrentPictureAsync(client));
        using (var download = await client.GetAsync($"/api/accounts/{ownerId}/picture"))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        }

        // A pending upload is not the picture that was seen, so it conflicts instead of removing the current one.
        var pending = Guid.NewGuid();
        using (var outage = fixture.CreateFactory(failAfterPut: true))
        using (var outageClient = outage.CreateClient())
        {
            outageClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.ServiceUnavailable,
                (await PutAsync(outageClient, pending, Image(SKEncodedImageFormat.Png, 70, 70), "image/png")).StatusCode);
        }

        using var conflict = await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{pending}");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(current, await CurrentPictureAsync(client));

        using var removed = await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{current}");
        Assert.Equal("Removed", (await removed.Content.ReadFromJsonAsync<AccountPictureReceipt>())!.RemovalReason);
        Assert.Null(await CurrentPictureAsync(client));
        using var replayed = await client.DeleteAsync($"/api/accounts/{ownerId}/picture/{current}");
        Assert.Equal("Removed", (await replayed.Content.ReadFromJsonAsync<AccountPictureReceipt>())!.RemovalReason);
        using var gone = await client.GetAsync($"/api/accounts/{ownerId}/picture");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    private static async Task<Guid?> CurrentPictureAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, byte[] bytes, string contentType)
    {
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await client.PutAsync($"/api/accounts/me/picture/{id}", body);
    }

    private static void AssertPictureHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    private static async Task SignInStaffAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        HttpClient client, string role)
    {
        using var scope = factory.Services.CreateScope();
        var email = $"staff.{Guid.NewGuid():N}@example.invalid";
        var result = await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(Guid.NewGuid(), email, "Password123!", role, null, "Amir Hakim", "0123456789"), CancellationToken.None);
        Assert.True(result.Created);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private static byte[] Image(SKEncodedImageFormat format, int width, int height, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
