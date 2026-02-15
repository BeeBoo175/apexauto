using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentInspectionFixtureAssertions
{
    public static async Task VerifyAsync(ServiceProvider provider, DevelopmentFixtureTarget target)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var drafts = await database.InspectionDrafts.AsNoTracking().ToArrayAsync();
        Assert.Equal(3, drafts.Length);
        Assert.Equal(3, await database.InspectionAssignments.CountAsync());
        foreach (var draft in drafts)
        {
            var technician = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == draft.BranchId && item.Role == AccountRoles.WorkshopTechnician);
            Assert.Equal(technician.Id, draft.TechnicianId);
            Assert.Equal(1, draft.AssignmentVersion);
        }

        var selected = drafts[0];
        var replacement = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new StaffProvisioningRequest(
            replacement, $"farid.hakim.{replacement:N}@example.invalid", DevelopmentFixtureManifest.Password,
            AccountRoles.WorkshopTechnician, selected.BranchId), CancellationToken.None);
        var manager = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == selected.BranchId && item.Role == AccountRoles.BranchManager);
        var change = await services.GetRequiredService<IInspectionAssignmentStore>().AssignAsync(manager.Id,
            selected.VehicleId, new InspectionAssignmentRequest(Guid.NewGuid(), replacement, 1, "Workshop reassignment"), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, change.Status);
        var saved = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        var reports = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        Assert.Contains(reports, report => report.Fixture.StartsWith("Inspection (", StringComparison.Ordinal) &&
            report.State == "drift; preserved" && report.RequiresAttention);
        Assert.Equal(saved, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
    }
}
