using Dealership.Application.Accounts;
using Dealership.Application.WorkCalendar;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.WorkCalendar;

internal sealed record WorkCalendarReadScope(AccountUser Actor, Guid? BranchId, string Employee)
{
    public CurrentActor CurrentActor => new(Actor.Id, Actor.Role, Actor.BranchId);

    public bool IsManager => Actor.Role is AccountRoles.BranchManager or AccountRoles.GeneralManager;

    public static async Task<WorkCalendarReadScope?> ResolveAsync(DealershipDbContext database, Guid actorId,
        WorkCalendarScope query, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == actorId && user.IsActive,
            cancellationToken);
        if (!query.IsValid || actor is null || actor.Role is not (AccountRoles.SalesAgent or
            AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        var branchId = actor.Role == AccountRoles.GeneralManager ? query.BranchId : actor.BranchId;
        if (actor.Role != AccountRoles.GeneralManager && (branchId is null ||
            query.BranchId is not null && query.BranchId != branchId))
        {
            return null;
        }
        if (branchId is not null &&
            !await database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken))
        {
            return null;
        }

        // Employee refinements never replace an employee's own-work predicate.
        if (actor.Role == AccountRoles.SalesAgent && query.Employee.Length > 0)
        {
            return null;
        }

        return new(actor, branchId, query.Employee);
    }
}
