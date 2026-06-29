using Dealership.Application.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// One administrative step applied to a fixture account; the operation id is the account event's identity, so a
// step that has already happened is recognised and never repeated.
public sealed record DevelopmentLifecycleStep(Guid OperationId, Guid StaffId, string Kind, Guid? BranchId,
    string Reason);

public static class DevelopmentLifecycleManifest
{
    // A sales agent with open work, suspended: handlers read as ineligible and queues show the badge.
    public static Guid SuspendedStaffId { get; } = Guid.Parse("841698d7-2709-43f6-88c0-6395d62de946");

    // A customer with bookings and sales, suspended: staff keep working their records and see the badge.
    public const string SuspendedCustomerEmail = "chong.wei.ling@example.invalid";

    // Accounts that exist only to carry a lifecycle: one moved between branches, one who has left. They sit outside
    // the baseline staff list so the per-branch staff lookups keep their meaning.
    public static IReadOnlyList<StaffProvisioningRequest> Staff { get; } = Array.AsReadOnly<StaffProvisioningRequest>(
    [
        new(Guid.Parse("e5b7fc91-923c-4558-9d34-4835fff046a0"), "nadia.ismail@example.invalid",
            DevelopmentFixtureManifest.Password, AccountRoles.SalesAgent, DevelopmentFixtureManifest.Branches[2].Id,
            "Nadia Ismail", "012-3531423"),
        new(Guid.Parse("820d49be-85c8-4bc4-962e-8a210669820b"), "hazwan.latif@example.invalid",
            DevelopmentFixtureManifest.Password, AccountRoles.SalesAgent, DevelopmentFixtureManifest.Branches[1].Id,
            "Hazwan Latif", "013-3538742")
    ]);

    public static IReadOnlyList<DevelopmentLifecycleStep> Steps { get; } = Array.AsReadOnly<DevelopmentLifecycleStep>(
    [
        new(Guid.Parse("fd3faca2-efd2-409a-bd3f-2a050c8fb070"), Staff[0].Id, "Transfer",
            DevelopmentFixtureManifest.Branches[1].Id, "Moved closer to home when the Petaling Jaya showroom grew"),
        new(Guid.Parse("ce931d9b-4ec8-4c5f-9fed-28ffadb9fc81"), Staff[1].Id, "Transfer",
            DevelopmentFixtureManifest.Branches[0].Id, "Covering the Glenmarie showroom during a vacancy"),
        new(Guid.Parse("d02745e4-d719-492e-9709-f9f9ded37ce9"), Staff[1].Id, "Deactivate", null,
            "Resigned and handed over every open record"),
        new(Guid.Parse("84a43b79-8acd-44c3-ad6b-25b72c398da3"), SuspendedStaffId, "Suspend", null,
            "Access paused while a handling complaint is reviewed")
    ]);

    public static Guid SuspendedCustomerOperationId { get; } = Guid.Parse("2ed5a29f-d889-4c3f-8c8e-8bc46ae9dce5");

    public const string SuspendedCustomerReason = "Chargeback dispute under review";
}

// Applies the lifecycle states the account views show, through the same services an Administrator uses, last in
// the run so earlier fixtures still see every account as active.
public sealed class DevelopmentLifecycleFixtures(DealershipDbContext database, StaffProvisioningService provisioning,
    StaffAccountAdministrationService administration, AccountSuspensionService suspensions)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var administratorId = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.Administrator).Id;
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var account in DevelopmentLifecycleManifest.Staff)
        {
            reports.Add(await ProvisionAsync(account, seed, cancellationToken));
        }

        foreach (var step in DevelopmentLifecycleManifest.Steps)
        {
            reports.Add(await ApplyAsync(step, administratorId, seed, cancellationToken));
        }

        reports.Add(await SuspendCustomerAsync(administratorId, seed, cancellationToken));
        return reports;
    }

    // The account is created once at its starting branch; later transfers and deactivation change it, so an
    // existing account is never provisioned again.
    private async Task<DevelopmentFixtureReport> ProvisionAsync(StaffProvisioningRequest account, bool seed,
        CancellationToken cancellationToken)
    {
        var name = $"Lifecycle account ({account.Email})";
        if (await database.Users.AnyAsync(user => user.Id == account.Id, cancellationToken))
        {
            return new(name, "matches", false);
        }

        if (!seed)
        {
            return new(name, "missing", true);
        }

        var created = await provisioning.ProvisionStaffAsync(account, cancellationToken);
        return new(name, created.Created ? "created" : "conflict; preserved", !created.Created);
    }

    private async Task<DevelopmentFixtureReport> ApplyAsync(DevelopmentLifecycleStep step, Guid administratorId,
        bool seed, CancellationToken cancellationToken)
    {
        var name = $"Lifecycle {step.Kind.ToLowerInvariant()} ({step.StaffId})";
        if (await database.AccountEvents.AnyAsync(item => item.Id == step.OperationId, cancellationToken))
        {
            return new(name, "matches", false);
        }

        if (!seed)
        {
            return new(name, "missing", true);
        }

        var status = step.Kind switch
        {
            "Transfer" => (await administration.TransferAsync(administratorId, step.StaffId,
                new AccountTransferRequest(step.OperationId, step.Reason, step.BranchId), cancellationToken))
                .Status.ToString(),
            "Deactivate" => (await administration.DeactivateAsync(administratorId, step.StaffId,
                new AccountDeactivationRequest(step.OperationId, step.Reason), cancellationToken)).Status.ToString(),
            _ => (await suspensions.SuspendAsync(administratorId, step.StaffId,
                new AccountSuspensionRequest(step.OperationId, step.Reason, false, false), cancellationToken))
                .ToString()
        };
        return status == "Succeeded" ? new(name, "created", false) : new(name, $"{step.Kind} {status}", true);
    }

    private async Task<DevelopmentFixtureReport> SuspendCustomerAsync(Guid administratorId, bool seed,
        CancellationToken cancellationToken)
    {
        var name = $"Lifecycle suspension ({DevelopmentLifecycleManifest.SuspendedCustomerEmail})";
        if (await database.AccountEvents.AnyAsync(item => item.Id == DevelopmentLifecycleManifest.SuspendedCustomerOperationId,
                cancellationToken))
        {
            return new(name, "matches", false);
        }

        var normalized = DevelopmentLifecycleManifest.SuspendedCustomerEmail.ToUpperInvariant();
        var customerId = await database.Users.AsNoTracking().Where(user => user.NormalizedEmail == normalized)
            .Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);
        if (!seed || customerId is null)
        {
            return new(name, "missing", true);
        }

        var status = await suspensions.SuspendAsync(administratorId, customerId.Value,
            new AccountSuspensionRequest(DevelopmentLifecycleManifest.SuspendedCustomerOperationId,
                DevelopmentLifecycleManifest.SuspendedCustomerReason, false, false), cancellationToken);
        return status == AccountSuspensionStatus.Succeeded ? new(name, "created", false)
            : new(name, $"suspend {status}", true);
    }
}
