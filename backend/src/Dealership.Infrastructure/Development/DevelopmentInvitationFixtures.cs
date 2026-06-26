using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public enum DevelopmentInvitationState
{
    Pending,
    Expired,
    Revoked
}

public sealed record DevelopmentInvitationFixture(Guid Id, string Email, string Role, int BranchIndex, string Name,
    string Mobile, DevelopmentInvitationState State);

// Pending, expired and revoked invitations for the Invited tab. Pending and revoked ones go through the canonical
// service; an expired one needs a past creation date, so it is built with the domain constructor. No mail is sent.
public sealed class DevelopmentInvitationFixtures(DealershipDbContext database, StaffInvitationService invitations,
    TimeProvider clock)
{
    public static IReadOnlyList<DevelopmentInvitationFixture> All { get; } = Array.AsReadOnly(new DevelopmentInvitationFixture[]
    {
        new(Guid.Parse("5b0c5d1e-7a34-4d0c-9a52-6f1c3a8e2b01"), "aiman.zulkifli@example.invalid",
            AccountRoles.SalesAgent, 0, "Aiman Zulkifli", "0123348821", DevelopmentInvitationState.Pending),
        new(Guid.Parse("c2d7e9a4-31b8-4f6e-8d05-9e4a7b6c1f02"), "siti.hajar@example.invalid",
            AccountRoles.WorkshopTechnician, 1, "Siti Hajar Ramli", "0165529043", DevelopmentInvitationState.Pending),
        new(Guid.Parse("e91a4c37-5d2b-4b8a-a6f3-0c7d8e5b3a03"), "hafiz.roslan@example.invalid",
            AccountRoles.SalesAgent, 2, "Hafiz Roslan", "0192237764", DevelopmentInvitationState.Expired),
        new(Guid.Parse("7f3b8d52-9c61-4e0a-b4d7-2a5e1c9f6d04"), "farah.nabila@example.invalid",
            AccountRoles.BranchManager, 0, "Farah Nabila Osman", "0114476018", DevelopmentInvitationState.Revoked)
    });

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var administrator = DevelopmentFixtureManifest.Staff.Single(item => item.Role == AccountRoles.Administrator);
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var fixture in All)
        {
            reports.Add(await RunOneAsync(fixture, administrator.Id, seed, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(DevelopmentInvitationFixture fixture, Guid administratorId,
        bool seed, CancellationToken cancellationToken)
    {
        var name = $"Invitation ({fixture.Email})";
        var existing = await database.StaffInvitations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == fixture.Id, cancellationToken);

        if (existing is not null)
        {
            var matches = existing.Email == fixture.Email && existing.Role == fixture.Role && existing.Name == fixture.Name;
            return new(name, matches ? "matches" : "drift; preserved", !matches);
        }

        if (!seed)
        {
            return new(name, "missing", true);
        }

        var branchId = DevelopmentFixtureManifest.Branches[fixture.BranchIndex].Id;
        if (fixture.State == DevelopmentInvitationState.Expired)
        {
            return await CreateExpiredAsync(fixture, name, administratorId, branchId, cancellationToken);
        }

        var request = new StaffInvitationRequest(fixture.Id, fixture.Email, fixture.Role, branchId, fixture.Name,
            fixture.Mobile);
        var invited = await invitations.InviteAsync(administratorId, request, cancellationToken);
        if (invited.Status != StaffInvitationStatus.Succeeded)
        {
            return new(name, $"invite {invited.Status}", true);
        }

        if (fixture.State == DevelopmentInvitationState.Revoked)
        {
            var revoked = await invitations.RevokeAsync(administratorId, fixture.Id, cancellationToken);
            if (revoked.Status != StaffInvitationStatus.Succeeded)
            {
                return new(name, $"revoke {revoked.Status}", true);
            }
        }

        await MarkDeliveredAsync(fixture.Id, cancellationToken);
        return new(name, "created", false);
    }

    private async Task<DevelopmentFixtureReport> CreateExpiredAsync(DevelopmentInvitationFixture fixture, string name,
        Guid administratorId, Guid branchId, CancellationToken cancellationToken)
    {
        var sent = clock.GetUtcNow().AddDays(-6);
        var validated = new ValidatedStaffInvitation(fixture.Id, fixture.Email, fixture.Role, branchId, fixture.Name,
            fixture.Mobile);
        database.StaffInvitations.Add(StaffInvitation.Create(validated, administratorId, sent));
        await database.SaveChangesAsync(cancellationToken);
        await MarkDeliveredAsync(fixture.Id, cancellationToken);
        return new(name, "created", false);
    }

    // Fixture invitations are shown, never mailed.
    private Task<int> MarkDeliveredAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return database.StaffInvitations.Where(item => item.Id == id && item.DeliveredAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, now), cancellationToken);
    }
}
