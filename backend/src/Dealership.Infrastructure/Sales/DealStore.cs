using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Inspections;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Sales;

public sealed class DealStore(DealershipDbContext database) : IDealStore
{
    private static readonly SalesOrderStatus[] Open =
        [SalesOrderStatus.Draft, SalesOrderStatus.PendingManagerApproval, SalesOrderStatus.Approved];

    private sealed record Actor(Guid Id, string Role, Guid? BranchId);

    public async Task<DealResult> CreateAsync(Guid actorId, DealCreateRequest request, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize replays of one operation so they see a single outcome.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"deal:" + request.Id}, 0))", cancellationToken);
        var actor = await LockActorAsync(actorId, cancellationToken);
        if (actor is not { Role: AccountRoles.SalesAgent, BranchId: not null })
        {
            return new(DealStatus.Forbidden);
        }

        var existing = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.Id,
            cancellationToken);
        if (existing is not null)
        {
            // An identical replay reconciles a lost receipt; the same identity for other contents is a conflict.
            return existing.SalesAgentId == actorId && existing.VehicleId == request.VehicleId
                ? new(DealStatus.Succeeded, await ViewAsync(existing, cancellationToken))
                : new(DealStatus.Conflict);
        }

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = {request.VehicleId} FOR UPDATE", cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT release_unavailable_reservation({request.VehicleId})", cancellationToken);
        var listing = await PublicListingQuery.Published(database).SingleOrDefaultAsync(row =>
            row.Vehicle.Id == request.VehicleId && row.Vehicle.BranchId == actor.BranchId, cancellationToken);
        var settings = await ReadSettingsAsync(cancellationToken);
        if (listing is null)
        {
            return new(DealStatus.NotFound);
        }

        if (settings is null)
        {
            return new(DealStatus.Unavailable);
        }

        var hold = await database.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.VehicleId == request.VehicleId && item.IsActive, cancellationToken);
        var customerId = await ResolveCustomerAsync(request, hold, cancellationToken);
        if (customerId is null || request.ReservationId is not null && hold?.Id != request.ReservationId)
        {
            return new(request.ReservationId is null ? DealStatus.NotFound : DealStatus.Conflict);
        }

        // Another customer's hold keeps the vehicle out of any deal; the holder's own hold is carried in.
        if (hold is not null && hold.CustomerId != customerId)
        {
            return new(DealStatus.Conflict);
        }

        var order = SalesOrder.CreateDraft(request.Id, request.VehicleId, customerId.Value, actorId, actor.BranchId.Value,
            listing.Revision.VehiclePrice, listing.Revision.RoadTax, settings,
            hold is null ? null : new DealReservation(hold.Id, hold.PromoId, hold.PromoCode, hold.Price.PromoDiscount,
                hold.Deposit), now, out var created);
        database.SalesOrders.Add(order);
        database.SalesOrderEvents.Add(created);
        return await SaveAsync(order, transaction, cancellationToken);
    }

    public async Task<DealResult> ReviseAsync(Guid actorId, Guid dealId, DealReviseRequest request, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var (order, failure) = await LoadForActionAsync(actorId, dealId, request.ExpectedVersion, agentOnly: true,
            cancellationToken);
        if (order is null)
        {
            return failure!;
        }

        var settings = await ReadSettingsAsync(cancellationToken);
        var listing = await PublicListingQuery.Published(database)
            .SingleOrDefaultAsync(row => row.Vehicle.Id == order.VehicleId, cancellationToken);
        if (settings is null || listing is null)
        {
            return new(settings is null ? DealStatus.Unavailable : DealStatus.Conflict);
        }

        var (warranty, warrantyFailure) = await ResolveWarrantyAsync(request.WarrantyPackageId,
            listing.Publication.SignedRevisionId, cancellationToken);
        var (tradeIn, tradeInFailure) = await ResolveTradeInAsync(order, request, cancellationToken);
        if (warrantyFailure is not null || tradeInFailure is not null)
        {
            return new(warrantyFailure ?? tradeInFailure!.Value);
        }

        return await ApplyAsync(order, transaction, () => order.Revise(warranty, request.NegotiatedDiscount, tradeIn,
            settings, actorId, Clamp(now, order)), cancellationToken);
    }

    public async Task<DealResult> TransitionAsync(Guid actorId, Guid dealId, DealAction action,
        DealTransitionRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var (order, failure, actor) = await LoadForTransitionAsync(actorId, dealId, request.ExpectedVersion, action,
            cancellationToken);
        if (order is null)
        {
            return failure!;
        }

        var at = Clamp(now, order);
        switch (action)
        {
            case DealAction.Submit:
                var settings = await ReadSettingsAsync(cancellationToken);
                var listed = await PublicListingQuery.Published(database)
                    .AnyAsync(row => row.Vehicle.Id == order.VehicleId, cancellationToken);
                if (settings is null || !listed)
                {
                    return new(settings is null ? DealStatus.Unavailable : DealStatus.Conflict);
                }

                var number = order.OrderNumber ?? await NextNumberAsync(at.Year, cancellationToken);
                return await ApplyAsync(order, transaction, () => order.Submit(settings, number, actor!.Id, at),
                    cancellationToken);
            case DealAction.Approve:
                return await ApplyAsync(order, transaction, () => order.Approve(actor!.Id, at), cancellationToken);
            case DealAction.Reject:
                return await ApplyAsync(order, transaction, () => order.Reject(actor!.Id, request.Reason!, at),
                    cancellationToken);
            default:
                return await ApplyAsync(order, transaction, () => order.Cancel(actor!.Id, request.Reason!, at),
                    cancellationToken);
        }
    }

    public async Task<DealResult> GetAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        var order = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == dealId,
            cancellationToken);
        return order is null || !CanSee(actor, order) ? new(DealStatus.NotFound)
            : new(DealStatus.Succeeded, await ViewAsync(order, cancellationToken));
    }

    public async Task<DealResult> ListAsync(Guid actorId, int page, int pageSize, string? status, string? number,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        var orders = database.SalesOrders.AsNoTracking().AsQueryable();
        orders = actor.Role switch
        {
            AccountRoles.SalesAgent => orders.Where(item => item.SalesAgentId == actor.Id),
            AccountRoles.BranchManager => orders.Where(item => item.BranchId == actor.BranchId),
            _ => orders
        };
        if (status is not null)
        {
            var wanted = Enum.Parse<SalesOrderStatus>(status);
            orders = orders.Where(item => item.Status == wanted);
        }

        if (number is not null)
        {
            orders = orders.Where(item => item.OrderNumber != null &&
                EF.Functions.ILike(item.OrderNumber, "%" + PromoAdministrationStore.EscapeLike(number) + "%", "\\"));
        }

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(item => new DealSummary(item.Id, item.OrderNumber,
                item.Status.ToString(), item.VehicleId, item.CustomerId, item.SalesAgentId, item.BranchId,
                item.BalancePayable, item.Version, item.UpdatedAtUtc)).ToListAsync(cancellationToken);
        return new(DealStatus.Succeeded, Page: new DealPage(items, total, page, pageSize));
    }

    // Scoping: agents see their own deals, branch managers their branch, general managers every deal.
    private static bool CanSee(Actor actor, SalesOrder order) => actor.Role switch
    {
        AccountRoles.SalesAgent => order.SalesAgentId == actor.Id,
        AccountRoles.BranchManager => order.BranchId == actor.BranchId,
        _ => true
    };

    private static DateTimeOffset Clamp(DateTimeOffset now, SalesOrder order) =>
        now < order.UpdatedAtUtc ? order.UpdatedAtUtc : now;

    private async Task<Actor?> LockActorAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return ToActor(user?.Id, user?.Role, user?.BranchId, user?.IsActive == true);
    }

    private async Task<Actor?> ReadActorAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actorId,
            cancellationToken);
        return ToActor(user?.Id, user?.Role, user?.BranchId, user?.IsActive == true);
    }

    private static Actor? ToActor(Guid? id, string? role, Guid? branchId, bool active)
    {
        var staff = role is AccountRoles.SalesAgent or AccountRoles.BranchManager or AccountRoles.GeneralManager;
        var scoped = role == AccountRoles.GeneralManager || branchId is not null;
        return id is null || !active || !staff || !scoped ? null : new Actor(id.Value, role!, branchId);
    }

    private async Task<(SalesOrder?, DealResult?)> LoadForActionAsync(Guid actorId, Guid dealId, long expectedVersion,
        bool agentOnly, CancellationToken cancellationToken)
    {
        var (order, failure, _) = await LoadForTransitionAsync(actorId, dealId, expectedVersion,
            agentOnly ? DealAction.Submit : DealAction.Cancel, cancellationToken);
        return (order, failure);
    }

    // Row-locks the deal, scopes it to the actor's role and enforces the version the editor was based on.
    private async Task<(SalesOrder?, DealResult?, Actor?)> LoadForTransitionAsync(Guid actorId, Guid dealId,
        long expectedVersion, DealAction action, CancellationToken cancellationToken)
    {
        var actor = await LockActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return (null, new(DealStatus.Forbidden), null);
        }

        var order = await database.SalesOrders.FromSqlInterpolated(
            $"SELECT * FROM \"SalesOrders\" WHERE \"Id\" = {dealId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (order is null || !CanSee(actor, order))
        {
            return (null, new(DealStatus.NotFound), null);
        }

        var manager = actor.Role is AccountRoles.BranchManager or AccountRoles.GeneralManager;
        var own = actor.Role == AccountRoles.SalesAgent && order.SalesAgentId == actor.Id;
        var allowed = action switch
        {
            DealAction.Approve or DealAction.Reject => manager,
            DealAction.Cancel => manager || own,
            _ => own
        };
        if (!allowed)
        {
            return (null, new(DealStatus.Forbidden), null);
        }

        return order.Version == expectedVersion ? (order, null, actor)
            : (null, new DealResult(DealStatus.Conflict, await ViewAsync(order, cancellationToken)), null);
    }

    private async Task<DealResult> ApplyAsync(SalesOrder order, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Func<SalesOrderEvent> act, CancellationToken cancellationToken)
    {
        try
        {
            database.SalesOrderEvents.Add(act());
        }
        catch (InvalidOperationException)
        {
            // The deal is not in a state that allows this action.
            return new(DealStatus.Conflict, await ViewAsync(order, cancellationToken));
        }
        catch (ArgumentException)
        {
            return new(DealStatus.Invalid);
        }

        return await SaveAsync(order, transaction, cancellationToken);
    }

    private async Task<DealResult> SaveAsync(SalesOrder order,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // One open deal per vehicle, and unique deal numbers, are enforced by the database.
            database.ChangeTracker.Clear();
            return new(DealStatus.Conflict);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(DealStatus.Succeeded, await ViewAsync(order, cancellationToken));
    }

    private async Task<DealView> ViewAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        var events = await database.SalesOrderEvents.AsNoTracking().Where(item => item.SalesOrderId == order.Id)
            .ToListAsync(cancellationToken);
        return DealView.From(order, events);
    }

    private Task<PurchaseSettings?> ReadSettingsAsync(CancellationToken cancellationToken) =>
        database.PurchaseSettings.FromSqlInterpolated($"SELECT * FROM \"PurchaseSettings\" WHERE \"Id\" = 1 FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)!;

    private async Task<Guid?> ResolveCustomerAsync(DealCreateRequest request, Reservation? hold,
        CancellationToken cancellationToken)
    {
        if (request.ReservationId is not null)
        {
            return hold?.CustomerId;
        }

        var normalized = request.CustomerEmail!.ToUpperInvariant();
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.NormalizedEmail == normalized &&
            item.Role == AccountRoles.Customer && item.IsActive && item.EmailConfirmed, cancellationToken);
        return user?.Id;
    }

    private async Task<(DealWarranty?, DealStatus?)> ResolveWarrantyAsync(Guid? packageId, Guid signedRevisionId,
        CancellationToken cancellationToken)
    {
        if (packageId is null)
        {
            return (null, null);
        }

        var package = await database.WarrantyPackages.FromSqlInterpolated(
            $"SELECT * FROM \"WarrantyPackages\" WHERE \"Id\" = {packageId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == signedRevisionId, cancellationToken);
        InspectionGrade? grade = signed.Report().Assessment.Grade;
        // Only an active package the vehicle's confirmed grade qualifies for can be sold.
        return package is null || !package.IsEligibleFor(grade) ? (null, DealStatus.Invalid)
            : (new DealWarranty(package.Id, package.Name, package.Price), null);
    }

    private async Task<(DealTradeIn?, DealStatus?)> ResolveTradeInAsync(SalesOrder order, DealReviseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TradeInAppointmentId is not { } appointmentId)
        {
            return (null, null);
        }

        var appointment = await database.IntakeAppointments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == appointmentId && item.OwnerId == order.CustomerId && item.CancelledAt == null, cancellationToken);
        // The allowance can never exceed the customer's own confirmed valuation.
        if (appointment is null || request.TradeInAllowance > appointment.Valuation.Maximum)
        {
            return (null, DealStatus.Invalid);
        }

        var taken = await database.SalesOrders.AnyAsync(item => item.TradeInAppointmentId == appointmentId &&
            item.Id != order.Id && Open.Contains(item.Status), cancellationToken);
        return taken ? (null, DealStatus.Conflict) : (new DealTradeIn(appointmentId, request.TradeInAllowance), null);
    }

    private async Task<string> NextNumberAsync(int year, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"DealNumberCounters\" (\"Year\", \"LastValue\") VALUES ({year}, 0) ON CONFLICT (\"Year\") DO NOTHING",
            cancellationToken);
        var counter = await database.DealNumberCounters.FromSqlInterpolated(
            $"SELECT * FROM \"DealNumberCounters\" WHERE \"Year\" = {year} FOR UPDATE").SingleAsync(cancellationToken);
        return counter.Next();
    }
}
