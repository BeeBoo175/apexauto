using Dealership.Domain.Common;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentIntakeOwnershipFixtures(DealershipDbContext database, AppointmentAssignmentService assignments)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var branch in DevelopmentIntakeManifest.Appointments.GroupBy(item => item.BranchId))
        {
            var manager = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == branch.Key && item.Role == AccountRoles.BranchManager).Id;
            var agent = DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == branch.Key && item.Role == AccountRoles.SalesAgent).Id;
            var booked = branch.Where(item => item.Status == StaffWorklistStatus.Booked).ToArray();
            for (var index = 0; index < booked.Length; index++)
            {
                var fixture = booked[index];
                var handlerId = (index % 3) switch { 0 => (Guid?)agent, 1 => manager, _ => null };
                if (!await database.IntakeAppointments.AnyAsync(item => item.Id == fixture.Id, cancellationToken))
                {
                    reports.Add(new($"Intake ownership ({fixture.RegistrationPlate})", "appointment missing; preserved", true));
                    continue;
                }

                var current = await database.AppointmentAssignments.AsNoTracking()
                    .Where(item => item.AppointmentId == fixture.Id).OrderByDescending(item => item.Version)
                    .FirstOrDefaultAsync(cancellationToken);
                if (current is not null || handlerId is null)
                {
                    var matches = current?.HandlerId == handlerId;
                    reports.Add(new($"Intake ownership ({fixture.RegistrationPlate})",
                        matches ? "matches" : "drift; preserved", !matches));
                    continue;
                }

                if (!seed)
                {
                    reports.Add(new($"Intake ownership ({fixture.RegistrationPlate})", "missing; preserved", true));
                    continue;
                }

                var result = await assignments.ExecuteAsync(handlerId.Value, fixture.Id, WorkOwnershipKind.Claimed,
                    new(fixture.Id, 0), cancellationToken);
                var succeeded = result.Status == AppointmentAssignmentStatus.Succeeded;
                reports.Add(new($"Intake ownership ({fixture.RegistrationPlate})",
                    succeeded ? "created" : "claim unavailable; preserved", !succeeded));
            }
        }

        return reports;
    }
}
