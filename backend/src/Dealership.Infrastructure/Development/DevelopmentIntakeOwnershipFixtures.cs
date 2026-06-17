using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentIntakeOwnershipFixtures(DealershipDbContext database, AppointmentAssignmentService assignments)
{
    private const string ReassignmentReason = "Moved to the colleague with capacity for this arrival.";

    private sealed record OwnershipStep(Guid ActorId, string Kind, Guid? HandlerId, string? Reason);

    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var branch in DevelopmentIntakeManifest.Appointments.GroupBy(item => item.BranchId))
        {
            var booked = branch.Where(item => item.Status == StaffWorklistStatus.Booked).ToArray();
            for (var index = 0; index < booked.Length; index++)
            {
                reports.Add(await RunOneAsync(seed, booked[index], index, cancellationToken));
            }
        }

        return reports;
    }

    // Six repeating cases: claimed by an agent, claimed by the manager, unclaimed, assigned by the manager,
    // reassigned to the colleague, and released after a claim.
    private static (IReadOnlyList<OwnershipStep> Steps, Guid? FinalHandler) Plan(DevelopmentIntakeFixture fixture, int index)
    {
        var manager = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == fixture.BranchId && item.Role == AccountRoles.BranchManager).Id;
        var agent = DevelopmentFixtureManifest.AgentFor(fixture.BranchId, fixture.Id).Id;
        var colleague = DevelopmentFixtureManifest.PrimaryAgent(fixture.BranchId).Id == agent
            ? DevelopmentFixtureManifest.AdditionalAgents.Single(item => item.BranchId == fixture.BranchId).Id
            : DevelopmentFixtureManifest.PrimaryAgent(fixture.BranchId).Id;
        return (index % 6) switch
        {
            0 => ([new(agent, WorkOwnershipKind.Claimed, null, null)], agent),
            1 => ([new(manager, WorkOwnershipKind.Claimed, null, null)], manager),
            3 => ([new(manager, WorkOwnershipKind.Assigned, agent, null)], agent),
            4 =>
            ([
                new(agent, WorkOwnershipKind.Claimed, null, null),
                new(manager, WorkOwnershipKind.Assigned, colleague, ReassignmentReason)
            ], colleague),
            5 =>
            ([
                new(agent, WorkOwnershipKind.Claimed, null, null),
                new(agent, WorkOwnershipKind.Released, null, null)
            ], null),
            _ => ([], null)
        };
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentIntakeFixture fixture, int index,
        CancellationToken cancellationToken)
    {
        var label = $"Intake ownership ({fixture.RegistrationPlate})";
        var (steps, finalHandler) = Plan(fixture, index);
        if (!await database.IntakeAppointments.AnyAsync(item => item.Id == fixture.Id, cancellationToken))
        {
            return new(label, "appointment missing; preserved", true);
        }

        var current = await CurrentAsync(fixture.Id, cancellationToken);
        if (current is not null || steps.Count == 0)
        {
            var matches = current?.HandlerId == finalHandler && (current?.Version ?? 0) == steps.Count;
            return new(label, matches ? "matches" : "drift; preserved", !matches);
        }

        if (!seed)
        {
            return new(label, "missing; preserved", true);
        }

        for (var step = 0; step < steps.Count; step++)
        {
            // The first step keeps the original operation identity so earlier seeded claims remain replayable.
            var operationId = step == 0 ? fixture.Id : DevelopmentInspectionScenarios.OperationId(fixture.Id, $"ownership-{step}");
            var result = await assignments.ExecuteAsync(steps[step].ActorId, fixture.Id, steps[step].Kind,
                new(operationId, step, steps[step].HandlerId, steps[step].Reason), cancellationToken);
            if (result.Status != AppointmentAssignmentStatus.Succeeded)
            {
                return new(label, "claim unavailable; preserved", true);
            }
        }

        return new(label, "created", false);
    }

    private Task<AppointmentAssignment?> CurrentAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        return database.AppointmentAssignments.AsNoTracking().Where(item => item.AppointmentId == appointmentId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
    }
}
