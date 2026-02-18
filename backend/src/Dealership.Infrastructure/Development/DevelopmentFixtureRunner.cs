using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Photos;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentFixtureReport(string Fixture, string State, bool RequiresAttention);

public sealed class DevelopmentFixtureRunner(DealershipDbContext database, DevelopmentCustomerFixtures customers,
    StaffProvisioningService staff, BranchProvisioningService branches, DevelopmentIntakeFixtures intake,
    IInspectionAssignmentStore assignments, IInspectionDraftSaveStore inspectionSaves, InspectionPhotoService inspectionPhotos,
    IPhotoImageValidator photoValidator)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> SeedAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await using var lockConnection = await DevelopmentFixtureLock.AcquireAsync(target, token);
        await intake.EnsureLocalStorageAsync(token);

        // A dedicated nonpooled connection owns the run lock; disposal releases it after failure or restart.
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var branch in DevelopmentFixtureManifest.Branches)
        {
            var provisioned = await branches.ProvisionBranchAsync(branch, token);
            reports.Add(Report(branch.Name, provisioned.Created, provisioned.MatchesRequestedState));
        }

        foreach (var customer in DevelopmentFixtureManifest.Customers)
        {
            reports.Add(await customers.SeedAsync(customer, token));
        }

        foreach (var account in DevelopmentFixtureManifest.Staff)
        {
            var provisioned = await staff.ProvisionStaffAsync(account, token);
            reports.Add(Report($"{account.Role} ({account.Email})", provisioned.Created, provisioned.MatchesRequestedState));
        }

        reports.AddRange(await intake.SeedAsync(target, token));
        reports.AddRange(await InspectionDraftsAsync(true, token));
        return reports;
    }

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> ReviewAsync(DevelopmentFixtureTarget target,
        CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var requested in DevelopmentFixtureManifest.Branches)
        {
            var branch = await database.Branches.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.Id == requested.Id, cancellationToken);
            reports.Add(new DevelopmentFixtureReport(requested.Name, branch is null ? "missing" :
                $"{branch.Name}; {branch.City}; {branch.State}; active={branch.IsActive}", branch is null));
        }

        foreach (var customer in DevelopmentFixtureManifest.Customers)
        {
            reports.Add(await customers.ReviewAsync(customer, cancellationToken));
        }

        foreach (var account in DevelopmentFixtureManifest.Staff)
        {
            reports.Add(await ReviewAccountAsync($"{account.Role} ({account.Email})", account.Id, cancellationToken));
        }

        reports.AddRange(await intake.ReviewAsync(cancellationToken));
        reports.AddRange(await InspectionDraftsAsync(false, cancellationToken));
        return reports;
    }

    private async Task<IReadOnlyList<DevelopmentFixtureReport>> InspectionDraftsAsync(bool seed,
        CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        var checkedIn = DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.CheckedIn);
        foreach (var fixture in checkedIn)
        {
            var technicianId = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == fixture.BranchId && item.Role == AccountRoles.WorkshopTechnician).Id;
            var vehicleId = await database.Vehicles.Where(item => item.IntakeAppointmentId == fixture.Id)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
            var draft = await database.InspectionDrafts.AsNoTracking()
                .SingleOrDefaultAsync(item => item.VehicleId == vehicleId, cancellationToken);
            var state = "missing";
            var attention = true;
            if (draft is not null)
            {
                attention = draft.Id != fixture.Id || draft.TechnicianId != technicianId || draft.AssignmentVersion != 1;
                state = attention ? "drift; preserved" : "matches";
            }
            if (seed && draft is null && vehicleId.HasValue)
            {
                var assigned = await assignments.AssignAsync(technicianId, vehicleId.Value,
                    new InspectionAssignmentRequest(fixture.Id, technicianId, 0, null), cancellationToken);
                attention = assigned.Status != InspectionAssignmentStatus.Succeeded;
                state = attention ? "claim unavailable; preserved" : "created";
            }
            reports.Add(new DevelopmentFixtureReport($"Inspection ({fixture.RegistrationPlate})", state, attention));
            if (!attention && vehicleId.HasValue)
            {
                reports.Add(await InspectionEntriesAsync(seed, fixture.Id, technicianId, fixture.RegistrationPlate, cancellationToken));
                reports.Add(await InspectionPhotoAsync(seed, fixture.Id, technicianId, fixture.RegistrationPlate, cancellationToken));
            }
        }
        return reports;
    }

    private async Task<DevelopmentFixtureReport> InspectionEntriesAsync(bool seed, Guid inspectionId, Guid technicianId,
        string plate, CancellationToken cancellationToken)
    {
        var latest = await database.InspectionDraftSaves.AsNoTracking().Where(save => save.InspectionId == inspectionId)
            .OrderByDescending(save => save.Version).FirstOrDefaultAsync(cancellationToken);
        var request = new InspectionDraftSaveRequest(inspectionId, 1, 0, DevelopmentInspectionManifest.Entries);
        if (latest is not null)
        {
            var expected = DevelopmentInspectionManifest.Entries.Select(entry => new Dealership.Domain.Inspections.InspectionDraftEntry(
                entry.PointCode, entry.Kind, entry.Severity, entry.EstimatedRepairCost, entry.Notes)).ToArray();
            var matches = latest.Id == request.OperationId && latest.Matches(technicianId, inspectionId, request, expected);
            return new($"Inspection entries ({plate})", matches ? "matches" : "drift; preserved", !matches);
        }

        if (!seed)
        {
            return new($"Inspection entries ({plate})", "missing", true);
        }

        var saved = await inspectionSaves.SaveAsync(technicianId, inspectionId, request, cancellationToken);
        var succeeded = saved.Status == InspectionAssignmentStatus.Succeeded;
        return new($"Inspection entries ({plate})", succeeded ? "created" : "save unavailable; preserved", !succeeded);
    }

    private async Task<DevelopmentFixtureReport> InspectionPhotoAsync(bool seed, Guid inspectionId, Guid technicianId,
        string plate, CancellationToken cancellationToken)
    {
        var id = DevelopmentInspectionManifest.PhotoId(inspectionId);
        using var content = DevelopmentFixturePhotos.Open(Dealership.Domain.Intake.PhotoAngle.Front);
        var validated = await photoValidator.ValidateAsync(content, "image/jpeg", cancellationToken)
            ?? throw new InvalidOperationException("The inspection fixture photo must pass canonical source validation.");
        var existing = await database.InspectionPhotos.AsNoTracking().SingleOrDefaultAsync(photo => photo.Id == id, cancellationToken);
        if (existing is not null)
        {
            var matches = existing.Matches(inspectionId, technicianId, 1, "EP-004", validated);
            if (!matches || existing.AvailableAt is not null || !seed)
            {
                var receipt = await inspectionPhotos.GetReceiptAsync(technicianId, inspectionId, id, cancellationToken);
                var complete = matches && receipt.Status == InspectionPhotoStatus.Available;
                return new($"Inspection photo ({plate})", complete ? "matches" : "drift; preserved", !complete);
            }
        }

        if (!seed)
        {
            return new($"Inspection photo ({plate})", "missing", true);
        }

        content.Position = 0;
        var result = await inspectionPhotos.UploadAsync(technicianId, inspectionId, id, 1, "EP-004", "image/jpeg",
            content, cancellationToken);
        var succeeded = result.Status == InspectionPhotoStatus.Available;
        return new($"Inspection photo ({plate})", succeeded ? "created" : "upload unavailable; preserved", !succeeded);
    }

    private async Task<DevelopmentFixtureReport> ReviewAccountAsync(string fixture, Guid? id,
        CancellationToken cancellationToken)
    {
        var account = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);
        var state = account is null ? "missing" :
            $"role={account.Role}; branch={account.BranchId}; active={account.IsActive}; verified={account.EmailConfirmed}";
        return new DevelopmentFixtureReport(fixture, state, account is null);
    }

    private static DevelopmentFixtureReport Report(string fixture, bool created, bool matches)
    {
        if (created)
        {
            return new DevelopmentFixtureReport(fixture, "created", false);
        }

        return new DevelopmentFixtureReport(fixture, matches ? "matches" : "drift; preserved", !matches);
    }
}
