using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.IntegrationTests;

public sealed class PurchaseSettingsAdministrationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task OnlyActiveAdministratorsReadAndUpdateWithOptimisticConcurrency()
    {
        var administrator = await AddUserAsync(AccountRoles.Administrator, true);
        var inactive = await AddUserAsync(AccountRoles.Administrator, false);
        var manager = await AddUserAsync(AccountRoles.GeneralManager, true);
        Assert.Equal(PurchaseSettingsAdministrationStatus.NotFound, (await ReadAsync(administrator)).Status);
        Assert.Equal(PurchaseSettingsAdministrationStatus.NotFound,
            (await UpdateAsync(administrator, Request(1))).Status);

        await using (var seed = NewContext())
        {
            seed.PurchaseSettings.Add(new(500m, 0.08m, administrator, DateTimeOffset.UtcNow.AddMinutes(-5)));
            await seed.SaveChangesAsync();
        }

        var current = await ReadAsync(administrator);
        Assert.Equal(PurchaseSettingsAdministrationStatus.Succeeded, current.Status);
        Assert.Equal((500m, 0.08m, 500m, 48, 1L), (current.Settings!.ProcessingFee, current.Settings.SstRate,
            current.Settings.MaxAgentDiscountAmount, current.Settings.ReservationHoldHours, current.Settings.Version));
        var bounds = current.Settings.Bounds;
        Assert.Equal((5000m, 0.20m, 5000m, 1, 168), (bounds.MaximumFee, bounds.MaximumSstRate, bounds.MaximumDiscount,
            bounds.MinimumHoldHours, bounds.MaximumHoldHours));
        foreach (var actor in new[] { inactive, manager, Guid.NewGuid(), Guid.Empty })
        {
            Assert.Equal(PurchaseSettingsAdministrationStatus.Forbidden, (await ReadAsync(actor)).Status);
            Assert.Equal(PurchaseSettingsAdministrationStatus.Forbidden, (await UpdateAsync(actor, Request(1))).Status);
        }

        Assert.Equal(PurchaseSettingsAdministrationStatus.Invalid,
            (await UpdateAsync(administrator, Request(1) with { ReservationHoldHours = 169 })).Status);
        Assert.Equal(PurchaseSettingsAdministrationStatus.Invalid, (await UpdateAsync(administrator, Request(0))).Status);

        var updated = await UpdateAsync(administrator, Request(1));
        Assert.Equal(PurchaseSettingsAdministrationStatus.Succeeded, updated.Status);
        Assert.Equal((600m, 0.06m, 750m, 72, 2L), (updated.Settings!.ProcessingFee, updated.Settings.SstRate,
            updated.Settings.MaxAgentDiscountAmount, updated.Settings.ReservationHoldHours, updated.Settings.Version));

        var stale = await UpdateAsync(administrator, Request(1) with { ProcessingFee = 700m });
        Assert.Equal(PurchaseSettingsAdministrationStatus.Conflict, stale.Status);
        Assert.Equal(600m, stale.Settings!.ProcessingFee);

        await using var verify = NewContext();
        var stored = await verify.PurchaseSettings.AsNoTracking().SingleAsync();
        Assert.Equal((600m, 2L, administrator), (stored.ProcessingFee, stored.Version, stored.UpdatedById));
    }

    private static PurchaseSettingsUpdateRequest Request(long version) => new(600m, 0.06m, 750m, 72, version);

    private Task<PurchaseSettingsAdministrationResult> ReadAsync(Guid actor) => ServiceAsync(service =>
        service.ReadAsync(actor, CancellationToken.None));

    private Task<PurchaseSettingsAdministrationResult> UpdateAsync(Guid actor, PurchaseSettingsUpdateRequest request) =>
        ServiceAsync(service => service.UpdateAsync(actor, request, CancellationToken.None));

    private async Task<PurchaseSettingsAdministrationResult> ServiceAsync(
        Func<PurchaseSettingsAdministrationService, Task<PurchaseSettingsAdministrationResult>> action)
    {
        await using var context = NewContext();
        return await action(new(new PurchaseSettingsAdministrationStore(context), TimeProvider.System));
    }

    private async Task<Guid> AddUserAsync(string role, bool active)
    {
        var id = Guid.NewGuid();
        var email = $"settings.{id:N}@example.invalid";
        await using var context = NewContext();
        context.Users.Add(new AccountUser
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            AccountUsername = TestUsernames.Next(),
            NormalizedUserName = email.ToUpperInvariant(),
            Role = role,
            IsActive = active
        });
        await context.SaveChangesAsync();
        return id;
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(database.RuntimeConnectionString).Options);
}
