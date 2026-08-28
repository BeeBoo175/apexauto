using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class StaffProvisioningTests(RegistrationDatabase database) : IClassFixture<RegistrationDatabase>
{
    [Fact]
    public async Task ProvisioningCreatesScopedStaffAndPreservesEditedRecordsOnReplay()
    {
        using var services = database.CreateServices();
        var branchId = Guid.NewGuid();
        var branch = new BranchProvisioningRequest(branchId, "ApexAuto Glenmarie",
            "12 Jalan Pengaturcara U1/51A", "Shah Alam", "Selangor", "+60355651234", "GLN");
        var staffId = Guid.NewGuid();
        var staff = new StaffProvisioningRequest(staffId,
            $"siti.aminah.{Guid.NewGuid():N}@example.invalid", "Password123!",
            AccountRoles.SalesAgent, branchId, "Siti Aminah", "0123456704");

        Assert.Equal(new BranchProvisioningResult(true, true), await ProvisionBranchAsync(services, branch));
        Assert.Equal(new ProvisioningResult(true, true), await ProvisionStaffAsync(services, staff));
        Assert.Equal(new BranchProvisioningResult(false, true), await ProvisionBranchAsync(services, branch));
        Assert.Equal(new ProvisioningResult(false, true), await ProvisionStaffAsync(services, staff));
        Assert.Equal(new ProvisioningResult(false, false), await ProvisionStaffAsync(services,
            staff with { Password = "DifferentPassword123!" }));
        Assert.Equal(new ProvisioningResult(false, false), await ProvisionStaffAsync(services,
            staff with { Name = "Siti Aminah Binti Yusof" }));

        string originalHash;
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            var account = await context.Users.SingleAsync(user => user.Id == staffId);
            originalHash = account.PasswordHash!;
            account.Role = AccountRoles.BranchManager;
            await context.SaveChangesAsync();
            await context.Branches.Where(candidate => candidate.Id == branchId)
                .ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.Phone, "+60355659999"));
        }

        using var restarted = database.CreateServices();
        Assert.Equal(new BranchProvisioningResult(false, false), await ProvisionBranchAsync(restarted, branch));
        Assert.Equal(new ProvisioningResult(false, false), await ProvisionStaffAsync(restarted,
            staff with { Password = "DifferentPassword123!" }));

        using var check = restarted.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Equal("+60355659999", (await saved.Branches.SingleAsync(candidate => candidate.Id == branchId)).Phone);
        var retained = await saved.Users.SingleAsync(user => user.Id == staffId);
        Assert.Equal(AccountRoles.BranchManager, retained.Role);
        Assert.Equal(originalHash, retained.PasswordHash);
        var profile = await saved.StaffProfiles.SingleAsync(candidate => candidate.UserId == staffId);
        Assert.Equal("Siti Aminah", profile.Name);
        Assert.Equal("0123456704", profile.Mobile);
    }

    [Fact]
    public async Task CompetingProvisioningCreatesOneBranchAndOneStaffAccount()
    {
        using var services = database.CreateServices();
        var branch = new BranchProvisioningRequest(Guid.NewGuid(), "ApexAuto Johor Jaya",
            "20 Jalan Dedap", "Johor Bahru", "Johor", "+6073512345", "JJ");
        var branches = await Task.WhenAll(ProvisionBranchAsync(services, branch), ProvisionBranchAsync(services, branch));
        Assert.Single(branches, result => result.Created);
        Assert.All(branches, result => Assert.True(result.MatchesRequestedState));
        var staff = new StaffProvisioningRequest(Guid.NewGuid(),
            $"azlan.rahim.{Guid.NewGuid():N}@example.invalid", "Password123!", AccountRoles.SalesAgent, branch.Id,
            "Azlan Rahim", "0123456705");
        var accounts = await Task.WhenAll(ProvisionStaffAsync(services, staff), ProvisionStaffAsync(services, staff));
        Assert.Single(accounts, result => result.Created);
        Assert.All(accounts, result => Assert.True(result.MatchesRequestedState));
    }

    [Fact]
    public async Task ProvisioningRejectsInactiveBranchAssignments()
    {
        using var services = database.CreateServices();
        var branchId = Guid.NewGuid();
        await ProvisionBranchAsync(services, new BranchProvisioningRequest(branchId, "ApexAuto Bayan Lepas",
            "16 Persiaran Bayan Indah", "Bayan Lepas", "Pulau Pinang", "+6046123456", "BL"));
        var email = $"muhammad.hafiz.{Guid.NewGuid():N}@example.invalid";
        var request = new StaffProvisioningRequest(Guid.NewGuid(), email, "Password123!",
            AccountRoles.SalesAgent, branchId, "Aina Sofia", "0123456706");

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
            await context.Branches.Where(branch => branch.Id == branchId)
                .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProvisionStaffAsync(services, request));
        using var check = services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await saved.Users.AnyAsync(user => user.Id == request.Id));
    }

    private static async Task<BranchProvisioningResult> ProvisionBranchAsync(ServiceProvider services,
        BranchProvisioningRequest branch)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BranchProvisioningService>();
        return await service.ProvisionBranchAsync(branch, CancellationToken.None);
    }

    private static async Task<ProvisioningResult> ProvisionStaffAsync(ServiceProvider services,
        StaffProvisioningRequest staff)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaffProvisioningService>();
        return await service.ProvisionStaffAsync(staff, CancellationToken.None);
    }
}
