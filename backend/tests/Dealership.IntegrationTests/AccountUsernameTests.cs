using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AccountUsernameTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OwnerChangesUsernameOnceLoggedReplaySafeAndUniqueIgnoringCase()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var customer = await IntakePhotoFixture.RegisterAsync(client);
        using var other = factory.CreateClient();
        var neighbour = await IntakePhotoFixture.RegisterAsync(other);
        var userId = (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.UserId;
        var operationId = Guid.NewGuid();

        var changed = await PutAsync(client, operationId, "New.Name_01");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.True(changed.Headers.CacheControl?.NoStore);
        Assert.Equal("New.Name_01", (await client.GetFromJsonAsync<AccountProfile>("/api/account/profile"))!.Username);
        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(client, operationId, "New.Name_01")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(client, Guid.NewGuid(), "New.Name_01")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var events = await database.AccountEvents.AsNoTracking().Where(item => item.TargetId == userId).ToArrayAsync();
            var recorded = Assert.Single(events);
            Assert.Equal(operationId, recorded.Id);
            Assert.Equal(userId, recorded.ActorId);
            var change = Assert.Single(recorded.Changes!);
            Assert.Equal(("Username", customer.Username, "New.Name_01"), (change.Field, change.Before, change.After));
        }

        var taken = await PutAsync(other, Guid.NewGuid(), "new.name_01");
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.True(taken.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(client, Guid.NewGuid(), neighbour.Username!.ToUpperInvariant())).StatusCode);
        var invalid = await PutAsync(client, Guid.NewGuid(), "no");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True(invalid.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(client, Guid.NewGuid(), "bad name!")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(client, Guid.Empty, "valid.name")).StatusCode);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid operationId, string username)
    {
        return client.PutAsJsonAsync("/api/account/username", new AccountUsernameRequest(operationId, username));
    }
}
