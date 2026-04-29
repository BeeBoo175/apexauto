using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Sales;

// Handling a hold: an agent claims it, a manager assigns or moves it, and releasing gives back the time it had left.
public sealed partial class DealStore
{
    private const string ReleasedClaimReason = "Claim released";

    public Task<DealResult> ClaimAsync(Guid actorId, Guid reservationId, DealClaimRequest request, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        HandleAsync(actorId, reservationId, request.Id, actorId, now, cancellationToken);

    public Task<DealResult> AssignAsync(Guid actorId, Guid reservationId, DealAssignRequest request, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        HandleAsync(actorId, reservationId, request.Id, request.AgentId, now, cancellationToken);

    // Claims, assigns or reassigns: the first two start the deal, the last moves the existing one.
    private async Task<DealResult> HandleAsync(Guid actorId, Guid reservationId, Guid dealId, Guid agentId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize replays of one operation so they see a single outcome.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"deal:" + dealId}, 0))", cancellationToken);
        var actor = await LockActorAsync(actorId, cancellationToken);
        var selfClaim = agentId == actorId;
        if (actor is null || selfClaim != (actor.Role == AccountRoles.SalesAgent))
        {
            return new(DealStatus.Forbidden);
        }

        var located = await database.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken);
        if (located is null || !InBranchScope(actor, located.BranchId))
        {
            return new(DealStatus.NotFound);
        }

        var existing = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == dealId,
            cancellationToken);
        if (existing is not null)
        {
            // An identical replay reconciles a lost receipt; the same identity for other contents is a conflict.
            return existing.SalesAgentId == agentId && existing.ReservationId == reservationId
                ? new(DealStatus.Succeeded, await ViewAsync(existing, cancellationToken))
                : new(DealStatus.Conflict);
        }

        var hold = await LockHoldAsync(located, cancellationToken);
        if (hold is null)
        {
            return new(DealStatus.Conflict);
        }

        var agent = selfClaim ? actor : await LockAssigneeAsync(agentId, hold.BranchId, cancellationToken);
        if (agent is null)
        {
            return new(DealStatus.Invalid);
        }

        var holdNow = await DatabaseNowAsync(cancellationToken);
        var sequence = await NextSequenceAsync(hold.Id, cancellationToken);
        try
        {
            if (hold.IsClaimed)
            {
                // Only a manager can move a hold that someone already handles; an agent cannot take it.
                return selfClaim ? new(DealStatus.Conflict)
                    : await ReassignAsync(hold, agentId, actor, sequence, holdNow, now, transaction, cancellationToken);
            }

            return await StartAsync(hold, dealId, agent, actor, sequence, holdNow, now, transaction, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new(DealStatus.Conflict);
        }
        catch (ArgumentException)
        {
            return new(DealStatus.Invalid);
        }
    }

    private async Task<DealResult> StartAsync(Reservation hold, Guid dealId, Actor agent, Actor actor, int sequence,
        DateTimeOffset holdNow, DateTimeOffset now, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var listing = await PublicListingQuery.Published(database).SingleOrDefaultAsync(row =>
            row.Vehicle.Id == hold.VehicleId && row.Vehicle.BranchId == hold.BranchId, cancellationToken);
        var settings = await ReadSettingsAsync(cancellationToken);
        if (listing is null)
        {
            return new(DealStatus.NotFound);
        }

        if (settings is null)
        {
            return new(DealStatus.Unavailable);
        }

        // The deal is the holder's: the customer, promo and deposit are carried in from the hold.
        var order = SalesOrder.CreateDraft(dealId, hold.VehicleId, hold.CustomerId, agent.Id, hold.BranchId,
            listing.Revision.VehiclePrice, listing.Revision.RoadTax, settings,
            new DealReservation(hold.Id, hold.PromoId, hold.PromoCode, hold.Price.PromoDiscount, hold.Deposit), now,
            out var created);
        var claimed = hold.Claim(agent.Id, actor.Id, sequence, holdNow);
        database.SalesOrders.Add(order);
        database.SalesOrderEvents.Add(created);
        database.ReservationEvents.Add(claimed);
        return await SaveAsync(order, transaction, cancellationToken);
    }

    private async Task<DealResult> ReassignAsync(Reservation hold, Guid agentId, Actor actor, int sequence,
        DateTimeOffset holdNow, DateTimeOffset now, IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var order = await LockOpenDealAsync(hold.Id, cancellationToken);
        if (order is null)
        {
            return new(DealStatus.Conflict);
        }

        // Giving the hold to the agent who already has it changes nothing, so a repeated request succeeds.
        if (hold.ClaimedById == agentId && order.SalesAgentId == agentId)
        {
            return new(DealStatus.Succeeded, await ViewAsync(order, cancellationToken));
        }

        var moved = hold.Reassign(agentId, actor.Id, sequence, holdNow);
        var changed = order.Reassign(agentId, actor.Id, Clamp(now, order));
        database.ReservationEvents.Add(moved);
        database.SalesOrderEvents.Add(changed);
        return await SaveAsync(order, transaction, cancellationToken);
    }

    // Gives the hold back with the time it had left; the draft deal built on the claim is cancelled with it.
    public async Task<DealResult> ReleaseAsync(Guid actorId, Guid reservationId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await LockActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        var located = await database.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken);
        if (located is null || !InBranchScope(actor, located.BranchId))
        {
            return new(DealStatus.NotFound);
        }

        var hold = await LockHoldAsync(located, cancellationToken);
        if (hold is not { IsClaimed: true })
        {
            return new(DealStatus.Conflict);
        }

        // An agent gives back only their own claim; managers act within their scope.
        if (actor.Role == AccountRoles.SalesAgent && hold.ClaimedById != actor.Id)
        {
            return new(DealStatus.Forbidden);
        }

        var order = await LockOpenDealAsync(hold.Id, cancellationToken);
        // Once the deal is submitted it is cancelled as a deal, which refunds the deposit, rather than released.
        if (order is not { Status: SalesOrderStatus.Draft })
        {
            return new(DealStatus.Conflict);
        }

        try
        {
            var released = hold.Release(actor.Id, await NextSequenceAsync(hold.Id, cancellationToken),
                await DatabaseNowAsync(cancellationToken));
            var cancelled = order.Cancel(actor.Id, ReleasedClaimReason, Clamp(now, order));
            database.ReservationEvents.Add(released);
            database.SalesOrderEvents.Add(cancelled);
        }
        catch (InvalidOperationException)
        {
            return new(DealStatus.Conflict);
        }

        return await SaveAsync(order, transaction, cancellationToken);
    }

    // Managers pick the assignee from the active agents of the hold's own branch; agents never list colleagues.
    public async Task<DealResult> AgentsAsync(Guid actorId, Guid reservationId, CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null or { Role: AccountRoles.SalesAgent })
        {
            return new(DealStatus.Forbidden);
        }

        var hold = await database.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken);
        if (hold is null || !InBranchScope(actor, hold.BranchId))
        {
            return new(DealStatus.NotFound);
        }

        var agents = await database.Users.AsNoTracking().Where(item =>
                item.Role == AccountRoles.SalesAgent && item.BranchId == hold.BranchId && item.IsActive)
            .OrderBy(item => item.Email).ThenBy(item => item.Id)
            .Select(item => new HoldAgent(item.Id, item.Email ?? string.Empty)).ToListAsync(cancellationToken);
        return new(DealStatus.Succeeded, Agents: agents);
    }

    // Cancelling a deal frees its vehicle and refunds the deposit held for it.
    private async Task<DealResult> CancelAsync(SalesOrder order, Actor actor, string reason, DateTimeOffset at,
        IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            database.SalesOrderEvents.Add(order.Cancel(actor.Id, reason, at));
        }
        catch (InvalidOperationException)
        {
            return new(DealStatus.Conflict, await ViewAsync(order, cancellationToken));
        }
        catch (ArgumentException)
        {
            return new(DealStatus.Invalid);
        }

        if (order.ReservationId is { } reservationId)
        {
            // The vehicle is locked before its hold, as every other release does.
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {order.VehicleId} FOR UPDATE", cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT release_unavailable_reservation({order.VehicleId})", cancellationToken);
            var hold = await database.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId, cancellationToken);
            if (hold.IsActive)
            {
                var recordedAt = await DatabaseNowAsync(cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                database.ReservationReleases.Add(new ReservationRelease(Guid.NewGuid(), hold,
                    ReservationReleaseKind.DealCancelled, actor.Id, reason, recordedAt));
            }
        }

        return await SaveAsync(order, transaction, cancellationToken);
    }

    private static bool InBranchScope(Actor actor, Guid branchId) => actor.Role switch
    {
        AccountRoles.SalesAgent or AccountRoles.BranchManager => actor.BranchId == branchId,
        _ => true
    };

    // The vehicle is locked before its hold, matching every other release and read, then the hold is judged afresh.
    private async Task<Reservation?> LockHoldAsync(Reservation located, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {located.VehicleId} FOR UPDATE", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT release_unavailable_reservation({located.VehicleId})", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Reservations\" WHERE \"Id\" = {located.Id} FOR UPDATE", cancellationToken);
        var hold = await database.Reservations.SingleAsync(item => item.Id == located.Id, cancellationToken);
        return hold.IsActive ? hold : null;
    }

    private async Task<Actor?> LockAssigneeAsync(Guid agentId, Guid branchId, CancellationToken cancellationToken)
    {
        var agent = await LockActorAsync(agentId, cancellationToken);
        return agent is { Role: AccountRoles.SalesAgent } && agent.BranchId == branchId ? agent : null;
    }

    private Task<SalesOrder?> LockOpenDealAsync(Guid reservationId, CancellationToken cancellationToken) =>
        database.SalesOrders.FromSqlInterpolated(
            $"SELECT * FROM \"SalesOrders\" WHERE \"ReservationId\" = {reservationId} AND \"Status\" <> 'Cancelled' FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)!;

    private async Task<int> NextSequenceAsync(Guid reservationId, CancellationToken cancellationToken) =>
        (await database.ReservationEvents.Where(item => item.ReservationId == reservationId)
            .MaxAsync(item => (int?)item.Sequence, cancellationToken) ?? 0) + 1;

    private Task<DateTimeOffset> DatabaseNowAsync(CancellationToken cancellationToken) =>
        database.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
}
