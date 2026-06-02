using Dealership.Application.Accounts;
using Dealership.Application.WorkCalendar;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

internal sealed record WorkCalendarReadScope(AccountUser Actor, Guid BranchId, string Employee)
{
    public CurrentActor CurrentActor => new(Actor.Id, Actor.Role, Actor.BranchId);

    public bool IsManager => Actor.Role is AccountRoles.BranchManager or AccountRoles.GeneralManager;

    public static async Task<WorkCalendarReadScope?> ResolveAsync(DealershipDbContext database, Guid actorId,
        WorkCalendarScope query, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == actorId && user.IsActive,
            cancellationToken);
        if (!query.IsValid || actor is null || actor.Role is not (AccountRoles.SalesAgent or
            AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        var branchId = actor.Role == AccountRoles.GeneralManager ? query.BranchId : actor.BranchId;
        if (branchId is null || actor.Role != AccountRoles.GeneralManager &&
            query.BranchId is not null && query.BranchId != branchId ||
            !await database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        // Employee refinements never replace an employee's own-work predicate.
        if (actor.Role is AccountRoles.SalesAgent or AccountRoles.WorkshopTechnician && query.Employee.Length > 0)
        {
            return null;
        }

        return new(actor, branchId.Value, query.Employee);
    }
}
