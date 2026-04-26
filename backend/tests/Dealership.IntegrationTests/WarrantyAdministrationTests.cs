using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class WarrantyAdministrationTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task OnlyActiveAdministratorsManagePackagesWithReplayConflictAndStaleProtection()
    {
        var administrator = await AddUserAsync(AccountRoles.Administrator, true);
        var inactive = await AddUserAsync(AccountRoles.Administrator, false);
        var manager = await AddUserAsync(AccountRoles.GeneralManager, true);
        var silver = new WarrantyCreateRequest(Guid.NewGuid(), " ApexShield Silver ", 12, 25000, 1200m, "C");

        foreach (var actor in new[] { inactive, manager, Guid.NewGuid(), Guid.Empty })
        {
            Assert.Equal(WarrantyAdministrationStatus.Forbidden, (await ListAsync(actor)).Status);
            Assert.Equal(WarrantyAdministrationStatus.Forbidden, (await CreateAsync(actor, silver)).Status);
        }

        foreach (var invalid in new[]
        {
            silver with { Id = Guid.Empty }, silver with { Name = " " }, silver with { MinimumGrade = "D" },
            silver with { MinimumGrade = "0" }, silver with { MinimumGrade = "a" }, silver with { DurationMonths = 0 },
            silver with { MileageCapKm = 500001 }, silver with { Price = 20000.01m }
        })
        {
            Assert.Equal(WarrantyAdministrationStatus.Invalid, (await CreateAsync(administrator, invalid)).Status);
        }

        Assert.Equal(WarrantyAdministrationStatus.Invalid, (await ListAsync(administrator, 1, 7)).Status);

        var first = await CreateAsync(administrator, silver);
        Assert.Equal(WarrantyAdministrationStatus.Succeeded, first.Status);
        Assert.Equal(("ApexShield Silver", "C", 1L), (first.Package!.Name, first.Package.MinimumGrade, first.Package.Version));
        var replay = await CreateAsync(administrator, silver);
        Assert.Equal(WarrantyAdministrationStatus.Succeeded, replay.Status);
        Assert.Equal(first.Package.Id, replay.Package!.Id);
        Assert.Equal(WarrantyAdministrationStatus.Conflict, (await CreateAsync(administrator, silver with { Price = 1300m })).Status);
        Assert.Equal(WarrantyAdministrationStatus.Conflict, (await CreateAsync(administrator, silver with { Id = Guid.NewGuid() })).Status);
        var gold = await CreateAsync(administrator, new(Guid.NewGuid(), "ApexShield Gold", 24, 50000, 2400m, "B"));
        Assert.Equal(WarrantyAdministrationStatus.Succeeded, gold.Status);

        var page = (await ListAsync(administrator)).Page!;
        Assert.Equal(2, page.Total);
        Assert.Equal(new[] { "ApexShield Silver", "ApexShield Gold" }, page.Items.Select(item => item.Name));
        var beyond = (await ListAsync(administrator, 2, 10)).Page!;
        Assert.Equal((2, 0), (beyond.Total, beyond.Items.Count));

        var change = new WarrantyUpdateRequest("ApexShield Silver", 18, 30000, 1500m, "B", false, 1);
        Assert.Equal(WarrantyAdministrationStatus.Forbidden, (await UpdateAsync(manager, first.Package.Id, change)).Status);
        Assert.Equal(WarrantyAdministrationStatus.NotFound, (await UpdateAsync(administrator, Guid.NewGuid(), change)).Status);
        Assert.Equal(WarrantyAdministrationStatus.Invalid, (await UpdateAsync(administrator, first.Package.Id, change with { MinimumGrade = "Z" })).Status);
        Assert.Equal(WarrantyAdministrationStatus.Conflict,
            (await UpdateAsync(administrator, first.Package.Id, change with { Name = "ApexShield Gold" })).Status);
        var updated = await UpdateAsync(administrator, first.Package.Id, change);
        Assert.Equal(WarrantyAdministrationStatus.Succeeded, updated.Status);
        Assert.Equal((18, "B", false, 2L), (updated.Package!.DurationMonths, updated.Package.MinimumGrade,
            updated.Package.IsActive, updated.Package.Version));

        var stale = await UpdateAsync(administrator, first.Package.Id, change with { Price = 1800m });
        Assert.Equal(WarrantyAdministrationStatus.Conflict, stale.Status);
        Assert.Equal(1500m, stale.Package!.Price);

        // Silver is now inactive grade B at RM 1,500; Gold is active grade B at RM 2,400.
        Assert.Equal(WarrantyAdministrationStatus.Succeeded,
            (await CreateAsync(administrator, new(Guid.NewGuid(), "ApexShield Platinum", 36, 75000, 3600m, "A"))).Status);
        Assert.Equal(WarrantyAdministrationStatus.Invalid, (await ListAsync(administrator, status: "Expired")).Status);
        Assert.Equal(WarrantyAdministrationStatus.Invalid, (await ListAsync(administrator, sort: "version")).Status);
        Assert.Equal(WarrantyAdministrationStatus.Invalid, (await ListAsync(administrator, direction: "asc")).Status);
        Assert.Equal("ApexShield Silver", Names(await ListAsync(administrator, status: "Inactive")));
        Assert.Equal("ApexShield Gold,ApexShield Platinum", Names(await ListAsync(administrator, status: "Active", sort: "name")));
        Assert.Equal("ApexShield Platinum", Names(await ListAsync(administrator, search: "PLAT")));
        Assert.Equal("", Names(await ListAsync(administrator, search: "%")));
        Assert.Equal("ApexShield Platinum,ApexShield Gold,ApexShield Silver",
            Names(await ListAsync(administrator, sort: "price", direction: "desc")));
        Assert.Equal("ApexShield Platinum,ApexShield Gold,ApexShield Silver",
            Names(await ListAsync(administrator, sort: "term", direction: "desc")));
        Assert.Equal("ApexShield Platinum,ApexShield Gold,ApexShield Silver",
            Names(await ListAsync(administrator, sort: "grade", direction: "asc")));
        Assert.Equal("ApexShield Silver,ApexShield Gold,ApexShield Platinum",
            Names(await ListAsync(administrator, sort: "status", direction: "asc")));
        var scoped = (await ListAsync(administrator, 1, 10, "apexshield", "Active", "mileage", "asc")).Page!;
        Assert.Equal((2, "ApexShield Gold"), (scoped.Total, scoped.Items[0].Name));
    }

    private static string Names(WarrantyAdministrationResult result) =>
        string.Join(',', result.Page!.Items.Select(item => item.Name));

    [Fact]
    public async Task RestrictedSqlEnforcesPackageShapeAndRetention()
    {
        var administrator = await AddUserAsync(AccountRoles.Administrator, true);
        var created = await CreateAsync(administrator, new(Guid.NewGuid(), "Schema Package", 12, 25000, 1200m, "C"));
        Assert.Equal(WarrantyAdministrationStatus.Succeeded, created.Status);

        foreach (var assignment in new[] { "\"DurationMonths\" = 0", "\"DurationMonths\" = 121", "\"MileageCapKm\" = 0",
            "\"MileageCapKm\" = 500001", "\"Price\" = -1", "\"Price\" = 20000.01", "\"Price\" = 'NaN'",
            "\"MinimumGrade\" = 'D'", "\"MinimumGrade\" = 'a'", "\"Version\" = 0", "\"Name\" = ' padded '", "\"Name\" = ''" })
        {
            await RejectedAsync($"UPDATE \"WarrantyPackages\" SET {assignment} WHERE \"Id\" = '{created.Package!.Id}'",
                PostgresErrorCodes.CheckViolation);
        }

        await RejectedAsync($"INSERT INTO \"WarrantyPackages\" SELECT gen_random_uuid(), \"Name\", \"DurationMonths\", " +
            "\"MileageCapKm\", \"Price\", \"MinimumGrade\", \"IsActive\", \"Version\" FROM \"WarrantyPackages\" " +
            $"WHERE \"Id\" = '{created.Package!.Id}'", PostgresErrorCodes.UniqueViolation);
        await RejectedAsync($"DELETE FROM \"WarrantyPackages\" WHERE \"Id\" = '{created.Package.Id}'",
            PostgresErrorCodes.InsufficientPrivilege);
    }

    private Task<WarrantyAdministrationResult> ListAsync(Guid actor, int page = 1, int pageSize = 25,
        string? search = null, string? status = null, string? sort = null, string? direction = null) =>
        ServiceAsync(service => service.ListAsync(actor, page, pageSize, search, status, sort, direction,
            CancellationToken.None));

    private Task<WarrantyAdministrationResult> CreateAsync(Guid actor, WarrantyCreateRequest request) =>
        ServiceAsync(service => service.CreateAsync(actor, request, CancellationToken.None));

    private Task<WarrantyAdministrationResult> UpdateAsync(Guid actor, Guid packageId, WarrantyUpdateRequest request) =>
        ServiceAsync(service => service.UpdateAsync(actor, packageId, request, CancellationToken.None));

    private async Task<WarrantyAdministrationResult> ServiceAsync(
        Func<WarrantyAdministrationService, Task<WarrantyAdministrationResult>> action)
    {
        await using var context = NewContext();
        return await action(new(new WarrantyAdministrationStore(context)));
    }

    private async Task RejectedAsync(string sql, string state)
    {
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, error.SqlState);
    }

    private async Task<Guid> AddUserAsync(string role, bool active)
    {
        var id = Guid.NewGuid();
        var email = $"warranty.{id:N}@example.invalid";
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
