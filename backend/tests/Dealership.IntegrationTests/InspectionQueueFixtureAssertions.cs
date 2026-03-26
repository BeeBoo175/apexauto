using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class InspectionQueueFixtureAssertions
{
    public static async Task VerifyAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var queues = services.GetRequiredService<IInspectionQueueStore>();
        var vehicles = await database.Vehicles.AsNoTracking().ToArrayAsync();
        var drafts = await database.InspectionDrafts.AsNoTracking().ToArrayAsync();
        var retainedApprovals = DevelopmentInspectionScenarios.All.Where(item => item.Stage == DevelopmentInspectionStage.RepairApprovalRetained)
            .Select(item => item.Repair(true).OperationId).ToArray();
        var requests = await database.InspectionRepairRequests.AsNoTracking()
            .Where(item => item.Budget > 0 && !retainedApprovals.Contains(item.Id) &&
                database.InspectionDrafts.Any(draft => draft.Id == item.InspectionId && draft.IsSigned &&
                    draft.Revision == item.Revision && draft.RepairVersion == item.Version) &&
                !database.InspectionRepairApprovals.Any(approval => approval.RequestId == item.Id))
            .ToArrayAsync();
        Assert.Equal(240, vehicles.Length);
        Assert.Equal(165, requests.Length);
        Assert.Contains(vehicles.GroupBy(item => item.CreatedAt), group => group.Count() == 3);
        foreach (var actor in DevelopmentFixtureManifest.Staff)
        {
            if (actor.Role is AccountRoles.SalesAgent or AccountRoles.Administrator)
            {
                Assert.Equal(InspectionAssignmentStatus.Forbidden,
                    (await queues.InspectionsAsync(actor.Id, new(), InspectionQueueStatus.All, CancellationToken.None)).Status);
                Assert.Equal(InspectionAssignmentStatus.Forbidden,
                    (await queues.RepairsAsync(actor.Id, new(), InspectionRepairQueueStatus.All, CancellationToken.None)).Status);
                continue;
            }

            var othersVehicles = drafts.Where(item => item.TechnicianId != actor.Id).Select(item => item.VehicleId).ToHashSet();
            var assignedInspections = drafts.Where(item => item.TechnicianId == actor.Id).Select(item => item.Id).ToHashSet();
            var expectedVehicles = vehicles.Where(item => (actor.BranchId == null || item.BranchId == actor.BranchId) &&
                    (actor.Role != AccountRoles.WorkshopTechnician || !othersVehicles.Contains(item.Id)))
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArray();
            var actualVehicles = new List<Guid>();
            for (var page = 1; actualVehicles.Count < expectedVehicles.Length; page++)
            {
                var result = await queues.InspectionsAsync(actor.Id, new(Page: page), InspectionQueueStatus.All, CancellationToken.None);
                Assert.Equal(InspectionAssignmentStatus.Succeeded, result.Status);
                Assert.Equal(expectedVehicles.Length, result.Queue!.Total);
                Assert.Equal(Math.Min(25, expectedVehicles.Length - actualVehicles.Count), result.Queue.Items.Count);
                actualVehicles.AddRange(result.Queue.Items.Select(item => item.VehicleId));
            }
            Assert.Equal(expectedVehicles, actualVehicles);

            var expectedRequests = requests.Where(item => (actor.BranchId == null || item.BranchId == actor.BranchId) &&
                    (actor.Role != AccountRoles.WorkshopTechnician || assignedInspections.Contains(item.InspectionId)))
                .OrderByDescending(item => item.RequestedAt).ThenByDescending(item => item.Id).Select(item => item.Id).ToArray();
            var actualRequests = new List<Guid>();
            Assert.Equal(expectedRequests.Length, (await queues.RepairsAsync(actor.Id, new(),
                InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Total);
            for (var page = 1; actualRequests.Count < expectedRequests.Length; page++)
            {
                var result = await queues.RepairsAsync(actor.Id, new(Page: page), InspectionRepairQueueStatus.PendingApproval, CancellationToken.None);
                Assert.Equal(expectedRequests.Length, result.Queue!.Total);
                Assert.Equal(Math.Min(25, expectedRequests.Length - actualRequests.Count), result.Queue.Items.Count);
                actualRequests.AddRange(result.Queue.Items.Select(item => item.RequestId));
                Assert.All(result.Queue.Items, item => Assert.Equal(InspectionRepairQueueStatus.PendingApproval, item.Status));
            }
            Assert.Equal(expectedRequests, actualRequests);
            Assert.Empty((await queues.InspectionsAsync(actor.Id, new(Page: int.MaxValue, PageSize: 50),
                InspectionQueueStatus.All, CancellationToken.None)).Queue!.Items);
            Assert.Empty((await queues.RepairsAsync(actor.Id, new(Page: int.MaxValue, PageSize: 50),
                InspectionRepairQueueStatus.All, CancellationToken.None)).Queue!.Items);

            var selectedBranch = DevelopmentFixtureManifest.Branches[0].Id;
            if (actor.BranchId is not null)
            {
                selectedBranch = DevelopmentFixtureManifest.Branches.First(item => item.Id != actor.BranchId).Id;
                Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.InspectionsAsync(actor.Id,
                    new(BranchId: selectedBranch), InspectionQueueStatus.All, CancellationToken.None)).Status);
                Assert.Equal(InspectionAssignmentStatus.Forbidden, (await queues.RepairsAsync(actor.Id,
                    new(BranchId: selectedBranch), InspectionRepairQueueStatus.All, CancellationToken.None)).Status);
                Assert.Single((await queues.InspectionsAsync(actor.Id, new(), InspectionQueueStatus.Unassigned, CancellationToken.None)).Queue!.Items);
                continue;
            }

            Assert.Equal(80, (await queues.InspectionsAsync(actor.Id, new(BranchId: selectedBranch),
                InspectionQueueStatus.All, CancellationToken.None)).Queue!.Total);
            Assert.Equal(55, (await queues.RepairsAsync(actor.Id, new(BranchId: selectedBranch),
                InspectionRepairQueueStatus.PendingApproval, CancellationToken.None)).Queue!.Total);
        }
    }
}
