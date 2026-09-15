using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// Its own class: the last-Administrator rule counts every active Administrator in the class database.
public sealed class AdversarialLastAdministratorTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Password = AdversarialWorld.AdministratorPassword;

    [Fact]
    public async Task TwoAdministratorsErasingEachOtherAtOnceNeverLeaveTheDealershipWithoutOne()
    {
        // Guarantee: concurrent erasures serialize on the Administrator locks, so at least one active Administrator
        // always remains and the survivor cannot erase themselves.
        using var world = AdversarialWorld.Create(fixture);
        var first = await world.AdministratorAsync();
        var second = await world.AdministratorAsync();

        var firstAction = world.EraseStaffAsync(first, second, Guid.NewGuid(), Password);
        var secondAction = world.EraseStaffAsync(second, first, Guid.NewGuid(), Password);
        var firstResult = await firstAction;
        var secondResult = await secondAction;

        var active = await ActiveAdministratorsAsync(world);
        Assert.Single(active);
        var survivor = active.Single();
        var erased = survivor == first ? second : first;
        Assert.Equal(AccountErasureStatus.Erased,
            survivor == first ? firstResult.Status : secondResult.Status);
        Assert.NotEqual(AccountErasureStatus.Erased,
            survivor == first ? secondResult.Status : firstResult.Status);
        Assert.NotNull((await world.UserAsync(erased)).ClosedAt);

        // The last Administrator is refused, whoever asks, and stays active and able to sign in.
        Assert.Equal(AccountErasureStatus.Refused,
            (await world.EraseStaffAsync(survivor, survivor, Guid.NewGuid(), Password)).Status);
        Assert.NotEqual(AccountErasureStatus.Erased,
            (await world.EraseStaffAsync(erased, survivor, Guid.NewGuid(), Password)).Status);
        Assert.Single(await ActiveAdministratorsAsync(world));
        Assert.NotNull((await world.UserAsync(survivor)).PasswordHash);
    }

    private static Task<List<Guid>> ActiveAdministratorsAsync(AdversarialWorld world) => world.RunAsync(services =>
        services.GetRequiredService<DealershipDbContext>().Users.AsNoTracking()
            .Where(user => user.Role == "Administrator" && user.IsActive && user.ClosedAt == null)
            .Select(user => user.Id).ToListAsync());
}
