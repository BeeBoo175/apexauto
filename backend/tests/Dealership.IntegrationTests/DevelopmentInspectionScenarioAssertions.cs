using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentInspectionScenarioAssertions
{
    public static async Task SeedInterruptedRevisionAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var scenario = DevelopmentInspectionScenarios.All.First(item => item.Stage == DevelopmentInspectionStage.Reinspected);
        var technician = Technician(scenario);
        var database = services.GetRequiredService<DealershipDbContext>();
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == scenario.Id).Select(item => item.Id).SingleAsync();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionAssignmentService>()
            .AssignAsync(technician, vehicleId, new(scenario.Id, technician, 0, null), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
            .SaveAsync(technician, scenario.Id, scenario.Save(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, scenario.Id, scenario.Sign(), CancellationToken.None)).Status);
    }

    public static async Task VerifyAsync(IServiceProvider provider, DevelopmentFixtureTarget target)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var queues = services.GetRequiredService<IInspectionQueueStore>();
        (InspectionGrade? Grade, VehicleStatus Status)[] expected =
        [
            (InspectionGrade.B, VehicleStatus.PendingInspection),
            (InspectionGrade.A, VehicleStatus.Available),
            (InspectionGrade.A, VehicleStatus.Available),
            (InspectionGrade.B, VehicleStatus.PendingInspection),
            (InspectionGrade.B, VehicleStatus.PendingInspection),
            (InspectionGrade.B, VehicleStatus.Available),
            (InspectionGrade.C, VehicleStatus.PendingInspection),
            (InspectionGrade.C, VehicleStatus.PendingInspection),
            (null, VehicleStatus.Rejected),
            (null, VehicleStatus.Rejected),
            (InspectionGrade.B, VehicleStatus.PendingInspection),
            (InspectionGrade.A, VehicleStatus.Available),
            (InspectionGrade.B, VehicleStatus.PendingInspection),
            (InspectionGrade.B, VehicleStatus.PendingInspection)
        ];

        var exampleIds = DevelopmentIntakeManifest.InspectionExamples.Select(item => item.Id).ToHashSet();
        foreach (var branch in DevelopmentInspectionScenarios.All.Where(item => exampleIds.Contains(item.Id))
            .GroupBy(item => item.Intake.BranchId))
        {
            var scenarios = branch.ToArray();
            Assert.Equal(14, scenarios.Length);
            var manager = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == branch.Key && item.Role == AccountRoles.BranchManager);
            var choices = await services.GetRequiredService<IInspectionAssignmentStore>().TechniciansAsync(manager.Id, 1, 25, CancellationToken.None);
            Assert.Equal(2, choices.Technicians!.Total);
            Assert.Contains(choices.Technicians.Items, item => item.Id == DevelopmentFixtureManifest.ReliefTechnicians.Single(relief => relief.BranchId == branch.Key).Id);
            for (var index = 0; index < scenarios.Length; index++)
            {
                var scenario = scenarios[index];
                var technician = Technician(scenario);
                var draft = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == scenario.Id);
                var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == draft.VehicleId);
                Assert.Equal(expected[index].Status, vehicle.Status);
                var saved = await database.InspectionDraftSaves.AsNoTracking().Where(item => item.InspectionId == scenario.Id)
                    .OrderByDescending(item => item.Version).FirstAsync();
                Assert.Equal(expected[index].Grade, saved.Receipt().Assessment.Grade);
                var signed = await reports.ReadAsync(technician, scenario.Id, draft.Revision, CancellationToken.None);
                if (index == 0)
                {
                    Assert.Null(saved.Receipt().ExaminedPointCodes);
                    Assert.Equal(InspectionAssignmentStatus.NotFound, signed.Status);
                    Assert.Equal(InspectionAssignmentStatus.Conflict, (await services.GetRequiredService<InspectionSignoffService>()
                        .SignAsync(technician, scenario.Id, scenario.Sign(), CancellationToken.None)).Status);
                    continue;
                }

                Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
                Assert.Equal(210, signed.Report!.Checklist.Count);
                Assert.Equal(expected[index].Grade, signed.Report.Assessment.Grade);
                Assert.Equal(expected[index].Status == VehicleStatus.Rejected, signed.Report.Assessment.IsRejected);
                Assert.Contains(signed.Report.Checklist, point => point.Code == "IE-040" && point.Kind == InspectionEntryKind.NotFitted);
                if (index is 8 or 9)
                {
                    Assert.Equal(InspectionAssignmentStatus.Conflict, (await services.GetRequiredService<InspectionRepairRequestService>()
                        .RequestAsync(technician, scenario.Id, scenario.Repair(), CancellationToken.None)).Status);
                }
            }

            var technicianId = Technician(scenarios[0]);
            var zero = await queues.RepairsAsync(technicianId, new(Search: scenarios[4].Intake.RegistrationPlate),
                InspectionRepairQueueStatus.All, CancellationToken.None);
            Assert.Equal(InspectionRepairQueueStatus.ApprovalNotRequired, Assert.Single(zero.Queue!.Items).Status);
            var completed = await queues.RepairsAsync(technicianId, new(Search: scenarios[5].Intake.RegistrationPlate),
                InspectionRepairQueueStatus.All, CancellationToken.None);
            var completion = Assert.Single(completed.Queue!.Items);
            Assert.Equal(InspectionRepairQueueStatus.Completed, completion.Status);
            Assert.Null(completion.ApprovalId);
            Assert.NotNull(completion.CompletionId);
            var approved = await queues.RepairsAsync(technicianId, new(Search: scenarios[3].Intake.RegistrationPlate),
                InspectionRepairQueueStatus.All, CancellationToken.None);
            Assert.Equal(InspectionRepairQueueStatus.Approved, Assert.Single(approved.Queue!.Items).Status);
            await VerifyRetainedHistoryAsync(services, scenarios[10], scenarios[11]);
            await VerifyRepairScopeAsync(services, scenarios[12], scenarios[13]);
        }

        var snapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        Assert.All(await services.GetRequiredService<DevelopmentFixtureRunner>().ReviewAsync(target, CancellationToken.None),
            report => Assert.False(report.RequiresAttention, $"{report.Fixture}: {report.State}"));
        Assert.Equal(snapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
    }

    public static async Task VerifyHumanEditPreservationAsync(IServiceProvider provider, DevelopmentFixtureTarget target)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var scenario = DevelopmentInspectionScenarios.All.First(item => item.Stage == DevelopmentInspectionStage.Draft);
        var edited = scenario.Save() with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, Entries = [] };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
            .SaveAsync(Technician(scenario), scenario.Id, edited, CancellationToken.None)).Status);
        var snapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        var results = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        Assert.Contains(results, report => report.Fixture.Contains(scenario.Intake.RegistrationPlate, StringComparison.Ordinal) &&
            report.State == "drift; preserved" && report.RequiresAttention);
        Assert.Equal(snapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
    }

    private static async Task VerifyRetainedHistoryAsync(IServiceProvider services,
        DevelopmentInspectionScenario superseded, DevelopmentInspectionScenario reinspected)
    {
        var technician = Technician(superseded);
        var queues = services.GetRequiredService<IInspectionQueueStore>();
        var work = await queues.RepairsAsync(technician, new(Search: superseded.Intake.RegistrationPlate),
            InspectionRepairQueueStatus.All, CancellationToken.None);
        Assert.Equal(2, work.Queue!.Total);
        Assert.Contains(work.Queue.Items, row => row.Status == InspectionRepairQueueStatus.Superseded && row.ApprovalId.HasValue);
        var pending = Assert.Single(work.Queue.Items, row => row.Status == InspectionRepairQueueStatus.PendingApproval);
        Assert.Equal(900m, pending.Budget);
        Assert.Null(pending.CoveringApprovalId);
        var denied = superseded.Completion() with { RequestId = pending.RequestId, RepairVersion = 2, OperationId = Guid.NewGuid() };
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await services.GetRequiredService<InspectionRepairCompletionService>()
            .CompleteAsync(technician, superseded.Id, denied, CancellationToken.None)).Status);

        var history = await services.GetRequiredService<IInspectionHistoryStore>().RevisionsAsync(technician,
            reinspected.Id, new(), CancellationToken.None);
        Assert.Equal(2, history.History!.TotalCount);
        var original = Assert.Single(history.History.Items, revision => revision.Revision == 1);
        Assert.Equal(InspectionGrade.B, original.Assessment.Grade);
        Assert.NotNull(original.Reopening);
        Assert.Equal(InspectionGrade.A, Assert.Single(history.History.Items, revision => revision.Revision == 2).Assessment.Grade);
    }

    public static async Task SeedPartialRepairAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var scenario = DevelopmentInspectionScenarios.All.First(item => item.Stage == DevelopmentInspectionStage.RepairScopeChanged);
        var technician = Technician(scenario);
        var manager = DevelopmentFixtureManifest.Staff.Single(item => item.BranchId == scenario.Intake.BranchId && item.Role == AccountRoles.BranchManager);
        var database = services.GetRequiredService<DealershipDbContext>();
        var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == scenario.Id).Select(item => item.Id).SingleAsync();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionAssignmentService>()
            .AssignAsync(technician, vehicleId, new(scenario.Id, technician, 0, null), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionDraftSaveService>()
            .SaveAsync(technician, scenario.Id, scenario.Save(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, scenario.Id, scenario.Sign(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionRepairRequestService>()
            .RequestAsync(technician, scenario.Id, scenario.Repair(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionRepairApprovalService>()
            .ApproveAsync(manager.Id, scenario.Id, scenario.Approval(), CancellationToken.None)).Status);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<InspectionRepairCompletionService>()
            .CompleteAsync(technician, scenario.Id, scenario.Completion(), CancellationToken.None)).Status);
        Assert.Equal(VehicleStatus.PendingInspection, (await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicleId)).Status);
    }

    private static async Task VerifyRepairScopeAsync(IServiceProvider services, DevelopmentInspectionScenario changed,
        DevelopmentInspectionScenario retained)
    {
        var queues = services.GetRequiredService<IInspectionQueueStore>();
        var technician = Technician(changed);
        var scope = await queues.RepairsAsync(technician, new(Search: changed.Intake.RegistrationPlate),
            InspectionRepairQueueStatus.All, CancellationToken.None);
        Assert.Equal(2, scope.Queue!.Total);
        var latest = Assert.Single(scope.Queue.Items, item => item.RepairVersion == 2);
        Assert.Equal(750m, latest.Budget);
        Assert.Equal(InspectionRepairQueueStatus.PendingApproval, latest.Status);
        Assert.Null(latest.CoveringApprovalId);
        var partial = Assert.Single(scope.Queue.Items, item => item.RepairVersion == 1);
        Assert.Equal(InspectionRepairQueueStatus.Superseded, partial.Status);
        Assert.NotNull(partial.CompletionId);
        var work = await services.GetRequiredService<IInspectionHistoryStore>().RepairsAsync(technician, changed.Id, 1, new(), CancellationToken.None);
        Assert.Equal(2, Assert.Single(work.History!.Items, item => item.Request.Version == 2).Request.Items.Count);

        var reuse = await queues.RepairsAsync(technician, new(Search: retained.Intake.RegistrationPlate),
            InspectionRepairQueueStatus.All, CancellationToken.None);
        var revised = Assert.Single(reuse.Queue!.Items, item => item.RepairVersion == 2);
        Assert.Equal(700m, revised.Budget);
        Assert.Equal(InspectionRepairQueueStatus.Approved, revised.Status);
        Assert.Null(revised.ApprovalId);
        Assert.Equal(retained.Approval().OperationId, revised.CoveringApprovalId);
    }

    private static Guid Technician(DevelopmentInspectionScenario scenario) =>
        DevelopmentFixtureManifest.PrimaryTechnician(scenario.Intake.BranchId).Id;
}
