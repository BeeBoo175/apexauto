using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleReadStore(DealershipDbContext database) : ISaleReadStore
{
    public async Task<SaleReadResult<SaleView>> GetAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await ScopeAsync(actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var facts = await (from reservation in database.Reservations.AsNoTracking()
                           join vehicle in database.Vehicles.AsNoTracking() on reservation.VehicleId equals vehicle.Id
                           join profile in database.CustomerProfiles.AsNoTracking()
                               on reservation.CustomerId equals profile.UserId
                           join customer in database.Users.AsNoTracking() on reservation.CustomerId equals customer.Id
                           join branch in database.Branches.AsNoTracking() on reservation.BranchId equals branch.Id
                           where reservation.Id == saleId
                           select new
                           {
                               reservation.Number,
                               reservation.CreatedAt,
                               vehicle.RegistrationPlate,
                               vehicle.ManufactureYear,
                               vehicle.Make,
                               vehicle.Model,
                               vehicle.Trim,
                               vehicle.Vin,
                               vehicle.MileageKilometres,
                               profile.LegalName,
                               profile.Mobile,
                               customer.Email,
                               Branch = branch.Name
                           }).SingleAsync(cancellationToken);
        var warranty = sale.WarrantyPackageId is { } packageId
            ? await database.WarrantyPackages.AsNoTracking().SingleAsync(item => item.Id == packageId, cancellationToken)
            : null;
        var tradeIn = sale.TradeInAppointmentId is { } appointmentId
            ? await database.IntakeAppointments.AsNoTracking()
                .SingleAsync(item => item.Id == appointmentId, cancellationToken)
            : null;
        var emails = await EmailsAsync([sale.AgentId, sale.DecidedById], cancellationToken);
        var context = new SaleContext(facts.Number, facts.RegistrationPlate,
            SaleLabels.Vehicle(facts.ManufactureYear, facts.Make, facts.Model, facts.Trim), facts.Vin,
            facts.MileageKilometres, facts.LegalName, facts.Mobile, facts.Email ?? string.Empty, facts.Branch,
            facts.CreatedAt, warranty?.DurationMonths, warranty?.MileageCapKm, tradeIn?.Valuation.Minimum,
            tradeIn?.Valuation.Maximum);
        return new(SaleStatus.Succeeded, new SaleView(sale.Id, DealSnapshot.Of(sale), Email(emails, sale.AgentId),
            Email(emails, sale.DecidedById), sale.CreatedAt, sale.UpdatedAt, context, SaleAccess.Actions(actor!, sale)));
    }

    // What the editor may offer: warranties the signed grade qualifies for and the customer's own trade-in cars.
    public async Task<SaleReadResult<SaleOptions>> OptionsAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await ScopeAsync(actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var grade = await SaleGrades.SignedGradeAsync(database, saleId, cancellationToken);
        var packages = await database.WarrantyPackages.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.Price).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var warranties = packages.Where(item => item.IsEligibleFor(grade)).Select(item => new WarrantyOption(item.Id,
            item.Name, item.DurationMonths, item.MileageCapKm, item.Price, item.MinimumGrade.ToString())).ToList();
        var taken = database.SalesOrders.Where(item => item.Id != saleId && item.TradeInAppointmentId != null &&
                item.Status != SalesOrderStatus.Expired && item.Status != SalesOrderStatus.Cancelled)
            .Select(item => item.TradeInAppointmentId);
        var appointments = await database.IntakeAppointments.AsNoTracking().Where(item =>
                item.OwnerId == sale.CustomerId && item.CancelledAt == null && !taken.Contains(item.Id))
            .OrderByDescending(item => item.StartsAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var tradeIns = appointments.Select(item => new TradeInOption(item.Id, SaleLabels.TradeIn(item),
            item.Valuation.Minimum, item.Valuation.Maximum)).ToList();
        return new(SaleStatus.Succeeded, new SaleOptions(warranties, tradeIns));
    }

    // Managers choose an assignee from the active agents of the sale's own branch; agents never list colleagues.
    public async Task<SaleReadResult<IReadOnlyList<SaleAgent>>> AgentsAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await ScopeAsync(actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        if (!SaleAccess.IsManager(actor!, sale.BranchId))
        {
            return new(SaleStatus.Forbidden);
        }

        var agents = await database.Users.AsNoTracking().Where(item =>
                item.Role == AccountRoles.SalesAgent && item.BranchId == sale.BranchId && item.IsActive)
            .OrderBy(item => item.Email).ThenBy(item => item.Id)
            .Select(item => new SaleAgent(item.Id, item.Email ?? string.Empty)).ToListAsync(cancellationToken);
        return new(SaleStatus.Succeeded, agents);
    }

    // Newest first, paged in SQL; the total and the rows come from one snapshot.
    public async Task<SaleReadResult<SaleHistoryPage>> HistoryAsync(Guid actorId, Guid saleId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var (status, _, sale) = await ScopeAsync(actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var entries = database.SalesOrderEvents.AsNoTracking().Where(item => item.SalesOrderId == saleId);
        var total = await entries.CountAsync(cancellationToken);
        var rows = await (from entry in entries
                          join released in database.ReservationReleases.AsNoTracking()
                              on entry.ReleaseId equals released.Id into releases
                          from release in releases.DefaultIfEmpty()
                          orderby entry.Sequence descending
                          select new
                          {
                              entry.Id,
                              entry.Sequence,
                              entry.Kind,
                              entry.RecordedAt,
                              entry.ActorId,
                              entry.AgentId,
                              entry.PreviousAgentId,
                              entry.FromStatus,
                              entry.ToStatus,
                              entry.Reason,
                              entry.Changes,
                              Refund = release == null ? (decimal?)null : release.Refund
                          }).ReadPageAsync(page, pageSize, total, cancellationToken);
        var emails = await EmailsAsync(rows.SelectMany(row => new[] { row.ActorId, row.AgentId, row.PreviousAgentId }),
            cancellationToken);
        var items = rows.Select(row => new SaleHistoryItem(row.Id, row.Sequence, row.Kind.ToString(), row.RecordedAt,
            Email(emails, row.ActorId) ?? "System", Email(emails, row.AgentId), Email(emails, row.PreviousAgentId),
            row.FromStatus?.ToString(), row.ToStatus.ToString(), row.Reason, row.Changes ?? [], row.Refund)).ToList();
        await transaction.CommitAsync(cancellationToken);
        return new(SaleStatus.Succeeded, new SaleHistoryPage(items, total, page, pageSize));
    }

    public async Task<SaleReadResult<SaleVersionView>> VersionAsync(Guid actorId, Guid saleId, long version,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await ScopeAsync(actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var entries = await database.SalesOrderEvents.AsNoTracking()
            .Where(item => item.SalesOrderId == saleId && (item.Sequence == version || item.Sequence == version - 1))
            .ToListAsync(cancellationToken);
        var entry = entries.SingleOrDefault(item => item.Sequence == version);
        if (entry is null)
        {
            return new(SaleStatus.NotFound);
        }

        var previous = entries.SingleOrDefault(item => item.Sequence == version - 1);
        var emails = await EmailsAsync([entry.ActorId, entry.AgentId], cancellationToken);
        return new(SaleStatus.Succeeded, new SaleVersionView(saleId, entry.Sequence, entry.Kind.ToString(),
            entry.RecordedAt, Email(emails, entry.ActorId) ?? "System", Email(emails, entry.AgentId), entry.Reason,
            entry.Snapshot, previous?.Snapshot));
    }
    private async Task<(SaleStatus Status, SaleStaff? Actor, SalesOrder? Sale)> ScopeAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var actor = await SaleStaffLocks.ReadAsync(database, actorId, cancellationToken);
        var branchActive = actor?.BranchId is not { } branchId ||
            await database.Branches.AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
        if (!SaleAccess.IsEligibleStaff(actor, branchActive))
        {
            return (SaleStatus.Forbidden, null, null);
        }

        var sale = await database.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == saleId,
            cancellationToken);
        return sale is null || !SaleAccess.CanSee(actor!, sale)
            ? (SaleStatus.NotFound, null, null)
            : (SaleStatus.Succeeded, actor, sale);
    }

    private async Task<Dictionary<Guid, string>> EmailsAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
        return await database.Users.AsNoTracking().Where(item => wanted.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Email ?? string.Empty, cancellationToken);
    }

    private static string? Email(Dictionary<Guid, string> emails, Guid? id) =>
        id is { } value ? emails.GetValueOrDefault(value, string.Empty) : null;
}
