using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.IntegrationTests;

public sealed class PromoAdministrationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task OnlyActiveAdministratorsManagePromosWithReplayConflictAndStaleProtection()
    {
        var administrator = await AddUserAsync(AccountRoles.Administrator, true);
        var inactive = await AddUserAsync(AccountRoles.Administrator, false);
        var manager = await AddUserAsync(AccountRoles.GeneralManager, true);
        var expiry = DateTimeOffset.UtcNow.AddDays(30);
        var created = new PromoCreateRequest(Guid.NewGuid(), " APEX500 ", 500m, expiry);

        foreach (var actor in new[] { inactive, manager, Guid.NewGuid(), Guid.Empty })
        {
            Assert.Equal(PromoAdministrationStatus.Forbidden, (await ListAsync(actor)).Status);
            Assert.Equal(PromoAdministrationStatus.Forbidden, (await CreateAsync(actor, created)).Status);
        }

        Assert.Equal(PromoAdministrationStatus.Invalid, (await CreateAsync(administrator, created with { Id = Guid.Empty })).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await CreateAsync(administrator, created with { Discount = 0m })).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await CreateAsync(administrator, created with { Code = "  " })).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, 0, 25)).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, 1, 7)).Status);

        var first = await CreateAsync(administrator, created);
        Assert.Equal(PromoAdministrationStatus.Succeeded, first.Status);
        Assert.Equal(("APEX500", 1L), (first.Promo!.Code, first.Promo.Version));
        var replay = await CreateAsync(administrator, created);
        Assert.Equal(PromoAdministrationStatus.Succeeded, replay.Status);
        Assert.Equal((first.Promo.Id, 1L), (replay.Promo!.Id, replay.Promo.Version));
        Assert.Equal(PromoAdministrationStatus.Conflict, (await CreateAsync(administrator, created with { Discount = 400m })).Status);
        Assert.Equal(PromoAdministrationStatus.Conflict,
            (await CreateAsync(administrator, created with { Id = Guid.NewGuid() })).Status);
        var second = await CreateAsync(administrator,
            new(Guid.NewGuid(), "apex500", 100m, expiry.AddDays(1), false));
        Assert.Equal(PromoAdministrationStatus.Succeeded, second.Status);

        var page = (await ListAsync(administrator)).Page!;
        Assert.Equal(2, page.Total);
        Assert.Equal(new[] { "apex500", "APEX500" }, page.Items.Select(item => item.Code));
        var beyond = (await ListAsync(administrator, 2, 10)).Page!;
        Assert.Equal((2, 0), (beyond.Total, beyond.Items.Count));

        var change = new PromoUpdateRequest(750m, expiry.AddDays(5), false, 1);
        Assert.Equal(PromoAdministrationStatus.Forbidden, (await UpdateAsync(manager, first.Promo.Id, change)).Status);
        Assert.Equal(PromoAdministrationStatus.NotFound, (await UpdateAsync(administrator, Guid.NewGuid(), change)).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await UpdateAsync(administrator, first.Promo.Id, change with { Discount = 100000.01m })).Status);
        var updated = await UpdateAsync(administrator, first.Promo.Id, change);
        Assert.Equal(PromoAdministrationStatus.Succeeded, updated.Status);
        Assert.Equal(("APEX500", 750m, false, 2L), (updated.Promo!.Code, updated.Promo.Discount, updated.Promo.IsActive, updated.Promo.Version));

        var stale = await UpdateAsync(administrator, first.Promo.Id, change with { Discount = 900m });
        Assert.Equal(PromoAdministrationStatus.Conflict, stale.Status);
        Assert.Equal(750m, stale.Promo!.Discount);

        Assert.Equal(PromoAdministrationStatus.Succeeded,
            (await CreateAsync(administrator, new(Guid.NewGuid(), "OLD_100", 50m, DateTimeOffset.UtcNow.AddDays(-1)))).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, status: "Bogus")).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, sort: "version")).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, direction: "desc")).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, sort: "code", direction: "up")).Status);
        Assert.Equal(PromoAdministrationStatus.Invalid, (await ListAsync(administrator, search: new string('x', 101))).Status);
        Assert.Equal("OLD_100", Codes(await ListAsync(administrator, status: "Expired")));
        Assert.Equal("APEX500,apex500", Codes(await ListAsync(administrator, status: "Inactive")));
        Assert.Equal("", Codes(await ListAsync(administrator, status: "Active")));
        Assert.Equal("APEX500,apex500", Codes(await ListAsync(administrator, search: " apex ", sort: "code", direction: "asc")));
        Assert.Equal("apex500,APEX500", Codes(await ListAsync(administrator, search: "APEX", sort: "code", direction: "desc")));
        Assert.Equal("OLD_100", Codes(await ListAsync(administrator, search: "d_1")));
        Assert.Equal("", Codes(await ListAsync(administrator, search: "%")));
        Assert.Equal("", Codes(await ListAsync(administrator, search: "D.1")));
        Assert.Equal("OLD_100,apex500,APEX500", Codes(await ListAsync(administrator, sort: "discount", direction: "asc")));
        Assert.Equal("APEX500,apex500,OLD_100", Codes(await ListAsync(administrator, sort: "status", direction: "asc")));
        Assert.Equal("OLD_100,APEX500,apex500", Codes(await ListAsync(administrator, sort: "status", direction: "desc")));
        var scoped = (await ListAsync(administrator, 1, 10, "apex", "Inactive", "code", "asc")).Page!;
        Assert.Equal((2, 2), (scoped.Total, scoped.Items.Count));
    }

    private static string Codes(PromoAdministrationResult result) =>
        string.Join(',', result.Page!.Items.Select(item => item.Code));

    private Task<PromoAdministrationResult> ListAsync(Guid actor, int page = 1, int pageSize = 25, string? search = null,
        string? status = null, string? sort = null, string? direction = null) =>
        ServiceAsync(service => service.ListAsync(actor, page, pageSize, search, status, sort, direction,
            CancellationToken.None));

    private Task<PromoAdministrationResult> CreateAsync(Guid actor, PromoCreateRequest request) =>
        ServiceAsync(service => service.CreateAsync(actor, request, CancellationToken.None));

    private Task<PromoAdministrationResult> UpdateAsync(Guid actor, Guid promoId, PromoUpdateRequest request) =>
        ServiceAsync(service => service.UpdateAsync(actor, promoId, request, CancellationToken.None));

    private async Task<PromoAdministrationResult> ServiceAsync(
        Func<PromoAdministrationService, Task<PromoAdministrationResult>> action)
    {
        await using var context = NewContext();
        return await action(new(new PromoAdministrationStore(context), TimeProvider.System));
    }

    private async Task<Guid> AddUserAsync(string role, bool active)
    {
        var id = Guid.NewGuid();
        var email = $"promo.{id:N}@example.invalid";
        await using var context = NewContext();
        context.Users.Add(new AccountUser
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
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
