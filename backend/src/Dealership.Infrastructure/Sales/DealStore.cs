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

    // Member-initialised so list filters and orderings translate to SQL.
    private sealed class Row
    {
        public Guid Id { get; init; }
        public string? OrderNumber { get; init; }
        public SalesOrderStatus Status { get; init; }
        public Guid VehicleId { get; init; }
        public string Plate { get; init; } = string.Empty;
        public string Make { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public int Year { get; init; }
        public Guid CustomerId { get; init; }
        public string Customer { get; init; } = string.Empty;
        public Guid AgentId { get; init; }
        public string? Agent { get; init; }
        public Guid BranchId { get; init; }
        public string Branch { get; init; } = string.Empty;
        public decimal Balance { get; init; }
        public decimal Negotiated { get; init; }
        public decimal? Limit { get; init; }
        public long Version { get; init; }
        public DateTimeOffset? Submitted { get; init; }
        public DateTimeOffset Updated { get; init; }
    }

    public async Task<DealResult> ListAsync(Guid actorId, DealListQuery query, CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        var scoped = Scope(actor, database.SalesOrders.AsNoTracking());
        var counts = new DealCounts(
            await scoped.CountAsync(item => Open.Contains(item.Status), cancellationToken),
            await scoped.CountAsync(item => item.Status == SalesOrderStatus.PendingManagerApproval, cancellationToken),
            await scoped.CountAsync(item => item.Status == SalesOrderStatus.Cancelled, cancellationToken));
        var rows = Filter(Project(scoped), query);
        var total = await rows.CountAsync(cancellationToken);
        var items = await Order(rows, query).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var summaries = items.Select(row => new DealSummary(row.Id, row.OrderNumber, row.Status.ToString(),
            DealNextStep.For(row.Status, row.OrderNumber is not null), row.VehicleId, row.Plate,
            $"{row.Year} {row.Make} {row.Model}", row.CustomerId, row.Customer, row.AgentId, row.Agent ?? string.Empty,
            row.BranchId, row.Branch, row.Balance, row.Negotiated, row.Limit, row.Version, row.Submitted, row.Updated))
            .ToList();
        return new(DealStatus.Succeeded, Page: new DealPage(summaries, total, query.Page, query.PageSize, counts));
    }

    // Eligible vehicles for a new deal: published in the agent's branch with no open deal, with any active hold.
    public async Task<DealResult> VehiclesAsync(Guid actorId, string? search, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is not { Role: AccountRoles.SalesAgent, BranchId: not null })
        {
            return new(DealStatus.Forbidden);
        }

        var rows = PublicListingQuery.Published(database).Where(row => row.Vehicle.BranchId == actor.BranchId &&
            !database.SalesOrders.Any(order => order.VehicleId == row.Vehicle.Id && Open.Contains(order.Status)));
        if (search is not null)
        {
            rows = rows.Where(row => EF.Functions.ILike(row.Vehicle.RegistrationPlate,
                "%" + PromoAdministrationStore.EscapeLike(search) + "%", "\\"));
        }

        var found = await rows.OrderBy(row => row.Vehicle.RegistrationPlate).Take(25).Select(row => new
        {
            row.Vehicle.Id,
            row.Vehicle.RegistrationPlate,
            row.Vehicle.ManufactureYear,
            row.Vehicle.Make,
            row.Vehicle.Model,
            row.Revision.VehiclePrice
        }).ToListAsync(cancellationToken);
        var ids = found.Select(item => item.Id).ToArray();
        var holds = await database.Reservations.AsNoTracking()
            .Where(item => ids.Contains(item.VehicleId) && item.IsActive && item.ExpiresAt > now)
            .Select(item => new { item.Id, item.VehicleId, item.CustomerId }).ToListAsync(cancellationToken);
        var holderIds = holds.Select(item => item.CustomerId).ToArray();
        var names = await database.CustomerProfiles.AsNoTracking().Where(item => holderIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.LegalName, cancellationToken);
        var vehicles = found.Select(item =>
        {
            var hold = holds.FirstOrDefault(candidate => candidate.VehicleId == item.Id);
            return new EligibleVehicle(item.Id, item.RegistrationPlate,
                $"{item.ManufactureYear} {item.Make} {item.Model}", item.VehiclePrice, hold is not null, hold?.Id,
                hold is null ? null : names.GetValueOrDefault(hold.CustomerId));
        }).ToList();
        return new(DealStatus.Succeeded, Vehicles: vehicles);
    }

    // What the editor may offer: warranties the vehicle's grade qualifies for and the customer's own trade-ins.
    public async Task<DealResult> OptionsAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return new(DealStatus.Forbidden);
        }

        var order = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == dealId,
            cancellationToken);
        if (order is null || !CanSee(actor, order))
        {
            return new(DealStatus.NotFound);
        }

        var listing = await PublicListingQuery.Published(database)
            .SingleOrDefaultAsync(row => row.Vehicle.Id == order.VehicleId, cancellationToken);
        if (listing is null)
        {
            return new(DealStatus.Conflict);
        }

        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == listing.Publication.SignedRevisionId, cancellationToken);
        InspectionGrade? grade = signed.Report().Assessment.Grade;
        var packages = await database.WarrantyPackages.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.Price).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var warranties = packages.Where(item => item.IsEligibleFor(grade)).Select(item => new WarrantyOption(item.Id,
            item.Name, item.DurationMonths, item.MileageCapKm, item.Price, item.MinimumGrade.ToString())).ToList();
        var taken = database.SalesOrders.Where(item => item.Id != order.Id && Open.Contains(item.Status) &&
            item.TradeInAppointmentId != null).Select(item => item.TradeInAppointmentId);
        var appointments = await database.IntakeAppointments.AsNoTracking().Where(item =>
            item.OwnerId == order.CustomerId && item.CancelledAt == null && !taken.Contains(item.Id))
            .OrderBy(item => item.StartsAt).ToListAsync(cancellationToken);
        var tradeIns = appointments.Select(item => new TradeInOption(item.Id,
            $"{item.RegistrationPlate} · {item.Valuation.ManufactureYear} {item.Valuation.Make} {item.Valuation.Model}",
            item.Valuation.Minimum, item.Valuation.Maximum)).ToList();
        return new(DealStatus.Succeeded, Options: new DealOptions(warranties, tradeIns));
    }

    private static IQueryable<SalesOrder> Scope(Actor actor, IQueryable<SalesOrder> orders) => actor.Role switch
    {
        AccountRoles.SalesAgent => orders.Where(item => item.SalesAgentId == actor.Id),
        AccountRoles.BranchManager => orders.Where(item => item.BranchId == actor.BranchId),
        _ => orders
    };

    private IQueryable<Row> Project(IQueryable<SalesOrder> orders) =>
        from order in orders
        join vehicle in database.Vehicles.AsNoTracking() on order.VehicleId equals vehicle.Id
        join profile in database.CustomerProfiles.AsNoTracking() on order.CustomerId equals profile.UserId
        join agent in database.Users.AsNoTracking() on order.SalesAgentId equals agent.Id
        join branch in database.Branches.AsNoTracking() on order.BranchId equals branch.Id
        select new Row
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            VehicleId = vehicle.Id,
            Plate = vehicle.RegistrationPlate,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.ManufactureYear,
            CustomerId = order.CustomerId,
            Customer = profile.LegalName,
            AgentId = order.SalesAgentId,
            Agent = agent.Email,
            BranchId = order.BranchId,
            Branch = branch.Name,
            Balance = order.BalancePayable,
            Negotiated = order.NegotiatedDiscount,
            Limit = order.DiscountLimitAtSubmit,
            Version = order.Version,
            Submitted = order.SubmittedAtUtc,
            Updated = order.UpdatedAtUtc
        };

    private static IQueryable<Row> Filter(IQueryable<Row> rows, DealListQuery query)
    {
        rows = query.View switch
        {
            "Active" => rows.Where(row => Open.Contains(row.Status)),
            "NeedsApproval" => rows.Where(row => row.Status == SalesOrderStatus.PendingManagerApproval),
            "History" => rows.Where(row => row.Status == SalesOrderStatus.Cancelled),
            _ => rows
        };
        if (query.Status is not null)
        {
            var status = Enum.Parse<SalesOrderStatus>(query.Status);
            rows = rows.Where(row => row.Status == status);
        }

        if (query.Number is not null)
        {
            rows = rows.Where(row => row.OrderNumber != null &&
                EF.Functions.ILike(row.OrderNumber, Contains(query.Number), "\\"));
        }

        if (query.Vehicle is not null)
        {
            rows = rows.Where(row => EF.Functions.ILike(row.Plate, Contains(query.Vehicle), "\\") ||
                EF.Functions.ILike(row.Make + " " + row.Model, Contains(query.Vehicle), "\\"));
        }

        if (query.Customer is not null)
        {
            rows = rows.Where(row => EF.Functions.ILike(row.Customer, Contains(query.Customer), "\\"));
        }

        if (query.Agent is not null)
        {
            rows = rows.Where(row => row.Agent != null && EF.Functions.ILike(row.Agent, Contains(query.Agent), "\\"));
        }

        if (query.Branch is not null)
        {
            rows = rows.Where(row => EF.Functions.ILike(row.Branch, Contains(query.Branch), "\\"));
        }

        if (query.MinimumBalance is { } minimumBalance)
        {
            rows = rows.Where(row => row.Balance >= minimumBalance);
        }

        if (query.MaximumBalance is { } maximumBalance)
        {
            rows = rows.Where(row => row.Balance <= maximumBalance);
        }

        if (query.MinimumDiscount is { } minimumDiscount)
        {
            rows = rows.Where(row => row.Negotiated >= minimumDiscount);
        }

        if (query.MaximumDiscount is { } maximumDiscount)
        {
            rows = rows.Where(row => row.Negotiated <= maximumDiscount);
        }

        if (query.Submitted is { From: { } submittedFrom, Until: { } submittedUntil })
        {
            rows = rows.Where(row => row.Submitted >= submittedFrom && row.Submitted < submittedUntil);
        }

        if (query.Updated is { From: { } updatedFrom, Until: { } updatedUntil })
        {
            rows = rows.Where(row => row.Updated >= updatedFrom && row.Updated < updatedUntil);
        }

        rows = query.NextStep switch
        {
            "Add terms and submit" => rows.Where(row => row.Status == SalesOrderStatus.Draft && row.OrderNumber == null),
            "Edit and resubmit" => rows.Where(row => row.Status == SalesOrderStatus.Draft && row.OrderNumber != null),
            "Awaiting manager approval" => rows.Where(row => row.Status == SalesOrderStatus.PendingManagerApproval),
            "Approved" => rows.Where(row => row.Status == SalesOrderStatus.Approved),
            "Cancelled" => rows.Where(row => row.Status == SalesOrderStatus.Cancelled),
            _ => rows
        };

        // One box for the common lookup: deal number, registration, vehicle or customer.
        return query.Search is null ? rows
            : rows.Where(row =>
                row.OrderNumber != null && EF.Functions.ILike(row.OrderNumber, Contains(query.Search), "\\") ||
                EF.Functions.ILike(row.Plate, Contains(query.Search), "\\") ||
                EF.Functions.ILike(row.Make + " " + row.Model, Contains(query.Search), "\\") ||
                EF.Functions.ILike(row.Customer, Contains(query.Search), "\\"));
    }

    private static string Contains(string value) => "%" + PromoAdministrationStore.EscapeLike(value) + "%";

    // Every ordering ends in the deal ID, so paging never repeats or skips a deal.
    private static IOrderedQueryable<Row> Order(IQueryable<Row> rows, DealListQuery query)
    {
        var down = query.Descending;
        return query.Sort switch
        {
            "number" => (down ? rows.OrderByDescending(row => row.OrderNumber) : rows.OrderBy(row => row.OrderNumber))
                .ThenBy(row => row.Id),
            "vehicle" => (down ? rows.OrderByDescending(row => row.Plate) : rows.OrderBy(row => row.Plate))
                .ThenBy(row => row.Id),
            "customer" => (down ? rows.OrderByDescending(row => row.Customer) : rows.OrderBy(row => row.Customer))
                .ThenBy(row => row.Id),
            "agent" => (down ? rows.OrderByDescending(row => row.Agent) : rows.OrderBy(row => row.Agent))
                .ThenBy(row => row.Id),
            "status" => (down ? rows.OrderByDescending(row => row.Status) : rows.OrderBy(row => row.Status))
                .ThenBy(row => row.Id),
            "balance" => (down ? rows.OrderByDescending(row => row.Balance) : rows.OrderBy(row => row.Balance))
                .ThenBy(row => row.Id),
            "discount" => (down ? rows.OrderByDescending(row => row.Negotiated) : rows.OrderBy(row => row.Negotiated))
                .ThenBy(row => row.Id),
            "submitted" => (down ? rows.OrderByDescending(row => row.Submitted) : rows.OrderBy(row => row.Submitted))
                .ThenBy(row => row.Id),
            "updated" => (down ? rows.OrderByDescending(row => row.Updated) : rows.OrderBy(row => row.Updated))
                .ThenBy(row => row.Id),
            _ => rows.OrderByDescending(row => row.Updated).ThenBy(row => row.Id)
        };
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
        return DealView.From(order, events) with { Context = await ContextAsync(order, events, cancellationToken) };
    }

    private async Task<DealContext> ContextAsync(SalesOrder order, IReadOnlyList<SalesOrderEvent> events,
        CancellationToken cancellationToken)
    {
        var vehicle = await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == order.VehicleId,
            cancellationToken);
        var profile = await database.CustomerProfiles.AsNoTracking()
            .SingleAsync(item => item.UserId == order.CustomerId, cancellationToken);
        var branch = await database.Branches.AsNoTracking().SingleAsync(item => item.Id == order.BranchId,
            cancellationToken);
        var staffIds = events.Select(item => item.ActorId).Append(order.SalesAgentId).ToList();
        if (order.DecidedById is { } decidedBy)
        {
            staffIds.Add(decidedBy);
        }

        var ids = staffIds.Append(order.CustomerId).Distinct().ToArray();
        var emails = await database.Users.AsNoTracking().Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Email ?? string.Empty, cancellationToken);
        var staff = staffIds.Distinct().ToDictionary(id => id, id => emails.GetValueOrDefault(id, string.Empty));

        string? tradeIn = null;
        decimal? tradeInMinimum = null;
        decimal? tradeInMaximum = null;
        if (order.TradeInAppointmentId is { } appointmentId)
        {
            var appointment = await database.IntakeAppointments.AsNoTracking()
                .SingleAsync(item => item.Id == appointmentId, cancellationToken);
            tradeIn = $"{appointment.RegistrationPlate} · {appointment.Valuation.ManufactureYear} " +
                $"{appointment.Valuation.Make} {appointment.Valuation.Model}";
            tradeInMinimum = appointment.Valuation.Minimum;
            tradeInMaximum = appointment.Valuation.Maximum;
        }

        var warranty = order.WarrantyPackageId is { } packageId
            ? await database.WarrantyPackages.AsNoTracking().SingleAsync(item => item.Id == packageId, cancellationToken)
            : null;
        var hold = order.ReservationId is { } reservationId
            ? await database.Reservations.AsNoTracking().SingleAsync(item => item.Id == reservationId, cancellationToken)
            : null;
        return new DealContext(vehicle.RegistrationPlate,
            $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} {vehicle.Trim}".Trim(), vehicle.Vin,
            vehicle.MileageKilometres, profile.LegalName, profile.Mobile, emails.GetValueOrDefault(order.CustomerId, string.Empty),
            emails.GetValueOrDefault(order.SalesAgentId, string.Empty), branch.Name, tradeIn, tradeInMinimum,
            tradeInMaximum, warranty?.DurationMonths, warranty?.MileageCapKm, hold?.CreatedAt, hold?.ExpiresAt, staff);
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
