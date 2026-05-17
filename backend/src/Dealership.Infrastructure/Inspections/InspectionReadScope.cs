using Dealership.Application.Accounts;
using Dealership.Domain.Inspections;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionReadScope
{
    // The caller owns the transaction and any resource-specific locks.
    public static async Task<AccountUser?> LockActorAsync(DealershipDbContext database, Guid actorId,
        CancellationToken cancellationToken, bool allowCustomer = false)
    {
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (allowCustomer && actor is { IsActive: true, Role: AccountRoles.Customer })
        {
            return actor;
        }

        if (actor is not { IsActive: true } ||
            actor.Role is not (AccountRoles.WorkshopTechnician or AccountRoles.BranchManager or AccountRoles.GeneralManager))
        {
            return null;
        }

        if (actor.Role != AccountRoles.GeneralManager && (actor.BranchId is null ||
            !await database.Branches.FromSqlInterpolated($"SELECT * FROM \"Branches\" WHERE \"Id\" = {actor.BranchId} FOR SHARE")
                .AnyAsync(branch => branch.IsActive, cancellationToken)))
        {
            return null;
        }
        return actor;
    }

    public static IQueryable<InspectionDraft> ReportDrafts(DealershipDbContext database, AccountUser actor)
    {
        var drafts = database.InspectionDrafts.AsNoTracking();
        if (actor.Role == AccountRoles.Customer)
        {
            // The composite sale/reservation FK retains the buyer and purchased vehicle, not the intake owner.
            return drafts.Where(draft => database.SalesOrders.Any(sale => sale.VehicleId == draft.VehicleId &&
                sale.CustomerId == actor.Id && sale.Status == SalesOrderStatus.Delivered));
        }

        if (actor.Role != AccountRoles.GeneralManager)
        {
            drafts = drafts.Where(draft => draft.BranchId == actor.BranchId);
        }

        if (actor.Role == AccountRoles.WorkshopTechnician)
        {
            drafts = drafts.Where(draft => draft.TechnicianId == actor.Id);
        }

        return drafts;
    }
}
