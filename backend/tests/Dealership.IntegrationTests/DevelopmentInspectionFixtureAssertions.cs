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
        Assert.Equal(3, await database.InspectionDraftSaves.CountAsync());
        Assert.Equal(3, await database.InspectionPhotos.CountAsync());
        Assert.Equal(3, await database.InspectionDraftPhotos.CountAsync());
        Assert.Equal(2, await database.InspectionSignedRevisions.CountAsync());
        Assert.Equal(1, await database.InspectionReopenings.CountAsync());
        Assert.Equal(1, await database.InspectionRepairRequests.CountAsync());
        foreach (var draft in drafts)
        {
            var technician = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == draft.BranchId && item.Role == AccountRoles.WorkshopTechnician);
            Assert.Equal(technician.Id, draft.TechnicianId);
            var reopened = draft.BranchId == DevelopmentFixtureManifest.Branches[0].Id;
            var repairing = draft.BranchId == DevelopmentFixtureManifest.Branches[1].Id;
            Assert.Equal(repairing, draft.IsSigned);
            Assert.Equal(repairing ? 1 : 0, draft.RepairVersion);
            Assert.Equal(reopened ? 2 : 1, draft.Revision);
            Assert.Equal(reopened ? 1 : 0, draft.ReopenedAfterSaveVersion);
            Assert.Equal(1, draft.AssignmentVersion);
            var savedDraft = await services.GetRequiredService<IInspectionAssignmentStore>()
                .ReadAsync(technician.Id, draft.Id, CancellationToken.None);
            Assert.Equal(1, savedDraft.Draft!.LatestSave!.Version);
            Assert.Equal(250m, savedDraft.Draft.LatestSave.Assessment.EstimatedRepairCost);
            Assert.Equal(2, savedDraft.Draft.LatestSave.Entries.Count);
            Assert.Equal([DevelopmentInspectionManifest.PhotoId(draft.Id)], savedDraft.Draft.LatestSave.PhotoIds);
            Assert.Equal(DevelopmentInspectionManifest.ExaminedPointCodes, savedDraft.Draft.LatestSave.ExaminedPointCodes);
            var photo = await services.GetRequiredService<InspectionPhotoService>().GetReceiptAsync(technician.Id,
                draft.Id, DevelopmentInspectionManifest.PhotoId(draft.Id), CancellationToken.None);
            Assert.Equal(InspectionPhotoStatus.Available, photo.Status);
            if (repairing)
            {
                var repair = await services.GetRequiredService<IInspectionRepairRequestStore>()
                    .ReadAsync(technician.Id, draft.Id, 1, CancellationToken.None);
                Assert.Equal(InspectionAssignmentStatus.Succeeded, repair.Status);
                Assert.Equal(250m, repair.Receipt!.Budget);
                Assert.Equal(DevelopmentInspectionManifest.RepairRequest(draft.Id).Items, repair.Receipt.Items);
                Assert.Equal(draft.RepairVersion, repair.Receipt.Version);
            }
            using var bytes = new MemoryStream();
            Assert.Equal(InspectionPhotoStatus.Available, (await services.GetRequiredService<InspectionPhotoService>()
                .DownloadAsync(technician.Id, draft.Id, photo.Receipt!.Id, bytes, CancellationToken.None)).Status);
            Assert.Equal(photo.Receipt.Bytes, bytes.Length);
            if (reopened)
            {
                var signed = await services.GetRequiredService<IInspectionReportStore>()
                    .ReadAsync(technician.Id, draft.Id, 1, CancellationToken.None);
                Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
                Assert.Equal(250m, signed.Report!.Assessment.EstimatedRepairCost);
                Assert.Equal(210, signed.Report.Checklist.Count);
                var receipt = await database.InspectionReopenings.SingleAsync(item => item.InspectionId == draft.Id);
                Assert.Equal(signed.Report.OperationId, receipt.SignedRevisionId);
                Assert.Equal(DevelopmentInspectionManifest.ReopenReason, receipt.Reason);
            }
        }

        var repairDraft = drafts.Single(draft => draft.BranchId == DevelopmentFixtureManifest.Branches[1].Id);
        var changedRepair = DevelopmentInspectionManifest.RepairRequest(repairDraft.Id) with
        {
            OperationId = Guid.NewGuid(),
            ExpectedVersion = 1,
            Budget = 275m
        };
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await services.GetRequiredService<IInspectionRepairRequestStore>()
            .RequestAsync(repairDraft.TechnicianId, repairDraft.Id, changedRepair, CancellationToken.None)).Status);
        var repairSnapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        var repairDrift = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        Assert.Contains(repairDrift, report => report.Fixture.StartsWith("Inspection repairs (", StringComparison.Ordinal) &&
            report.State == "drift; preserved" && report.RequiresAttention);
        Assert.Equal(repairSnapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));

        var selected = drafts.First(draft => draft.Revision == 1 && !draft.IsSigned);
        var edit = await services.GetRequiredService<IInspectionDraftSaveStore>().SaveAsync(selected.TechnicianId,
            selected.Id, new(Guid.NewGuid(), 1, 1, []), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, edit.Status);
        var edited = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider);
        var drift = await services.GetRequiredService<DevelopmentFixtureRunner>().SeedAsync(target, CancellationToken.None);
        Assert.Contains(drift, report => report.Fixture.StartsWith("Inspection entries (", StringComparison.Ordinal) &&
            report.State == "drift; preserved" && report.RequiresAttention);
        Assert.Equal(edited, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(provider));
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
