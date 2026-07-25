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

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await PutAsync(customerClient, [1, 2, 3], "text/plain")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(customerClient, [1, 2, 3], "image/jpeg")).StatusCode);
        var wide = Image(SKEncodedImageFormat.Png, 600, 300);
        var stored = await PutAsync(customerClient, wide, "image/png");
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        var first = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId;
        Assert.NotNull(first);

        var thumbnail = await customerClient.GetAsync($"/api/accounts/{customerId}/picture");
        Assert.Equal("image/jpeg", thumbnail.Content.Headers.ContentType?.MediaType);
        using var decoded = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync());
        Assert.Equal((256, 256), (decoded.Width, decoded.Height));
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.GetAsync($"/api/accounts/{customerId}/picture")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.DeleteAsync($"/api/accounts/{customerId}/picture")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(customerClient, Image(SKEncodedImageFormat.Jpeg, 40, 40), "image/jpeg")).StatusCode);
        var second = (await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId;
        Assert.NotEqual(first, second);

        using var staffClient = factory.CreateClient();
        await SignInStaffAsync(factory, staffClient, AccountRoles.GeneralManager);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync($"/api/accounts/{customerId}/picture")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.DeleteAsync($"/api/accounts/{customerId}/picture")).StatusCode);

        using var administratorClient = factory.CreateClient();
        await SignInStaffAsync(factory, administratorClient, AccountRoles.Administrator);
        Assert.Equal(HttpStatusCode.NoContent, (await administratorClient.DeleteAsync($"/api/accounts/{customerId}/picture")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customerClient.GetAsync($"/api/accounts/{customerId}/picture")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<Dealership.Infrastructure.Persistence.DealershipDbContext>();
            var removal = Assert.Single(database.AccountEvents.Where(item => item.TargetId == customerId));
            Assert.Equal(Dealership.Domain.Accounts.AccountEventKind.PictureRemoved, removal.Kind);
            Assert.NotEqual(customerId, removal.ActorId);
        }

        Assert.Null((await customerClient.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.PictureId);
        Assert.Equal(HttpStatusCode.NotFound, (await customerClient.DeleteAsync($"/api/accounts/{customerId}/picture")).StatusCode);
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

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, byte[] bytes, string contentType)
    {
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await client.PutAsync("/api/accounts/me/picture", body);
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

    private static byte[] Image(SKEncodedImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
