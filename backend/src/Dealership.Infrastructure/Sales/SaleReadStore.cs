using Dealership.Infrastructure.Accounts;
using System.Data;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleReadStore(DealershipDbContext database) : ISaleReadStore
{
    public async Task<SaleReadResult<SaleVehicleReference>> VehicleAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var reference = await (from reservation in database.Reservations.AsNoTracking()
                               join publication in database.ListingPublications.AsNoTracking()
                                   on reservation.PublicationId equals publication.Id
                               join revision in database.ListingRevisions.AsNoTracking()
                                   on publication.RevisionId equals revision.Id
                               join vehicle in database.Vehicles.AsNoTracking() on reservation.VehicleId equals vehicle.Id
                               join branch in database.Branches.AsNoTracking() on reservation.BranchId equals branch.Id
                               where reservation.Id == saleId
                               select new SaleVehicleReference(saleId, vehicle.Make, vehicle.Model, vehicle.Trim,
                                   vehicle.ManufactureYear, vehicle.MileageKilometres, branch.Name,
                                   publication.RecordedAt, new SaleVehicleListing(vehicle.Id, revision.VehiclePrice,
                                       revision.RoadTax, revision.Transmission, revision.Fuel, revision.Description,
                                       revision.PhotoIds))).SingleAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, reference);
    }

    public async Task<SaleReadResult<SaleView>> GetAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
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
                               reservation.ExpiresAt,
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
        var names = await NamesAsync([sale.AgentId, sale.DecidedById], cancellationToken);
        var context = new SaleContext(facts.Number, facts.RegistrationPlate,
            SaleLabels.Vehicle(facts.ManufactureYear, facts.Make, facts.Model, facts.Trim), facts.Vin,
            facts.MileageKilometres, facts.LegalName, facts.Mobile, facts.Email ?? string.Empty, facts.Branch,
            facts.CreatedAt, warranty?.DurationMonths, warranty?.MileageCapKm, tradeIn?.Valuation.Minimum,
            tradeIn?.Valuation.Maximum, (long)(facts.ExpiresAt - facts.CreatedAt).TotalSeconds, sale.CustomerId);
        return new(SaleResultStatus.Succeeded, new SaleView(sale.Id, SaleSnapshot.Of(sale), Name(names, sale.AgentId),
            Name(names, sale.DecidedById), sale.CreatedAt, sale.UpdatedAt, context, SaleAccess.Actions(actor!, sale)));
    }

    // What the editor may offer: warranties the signed grade qualifies for and the customer's eligible trade-ins.
    public async Task<SaleReadResult<SaleOptions>> OptionsAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        var grade = await SaleGrades.SignedGradeAsync(database, saleId, cancellationToken);
        var packages = await database.WarrantyPackages.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.Price).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var warranties = packages.Where(item => item.IsEligibleFor(grade)).Select(item => new WarrantyOption(item.Id,
            item.Name, item.DurationMonths, item.MileageCapKm, item.Price, item.MinimumGrade.ToString())).ToList();
        var taken = SaleTradeIns.TakenByOtherSales(database, saleId);
        var appointments = await SaleTradeIns.Eligible(database, sale).Where(item => !taken.Contains(item.Id))
            .OrderByDescending(item => item.StartsAt).ThenBy(item => item.Id).Take(SaleTradeIns.OptionLimit)
            .ToListAsync(cancellationToken);
        var tradeIns = appointments.Select(item => new TradeInOption(item.Id, SaleLabels.TradeIn(item),
            item.Valuation.Minimum, item.Valuation.Maximum)).ToList();
        return new(SaleResultStatus.Succeeded, new SaleOptions(warranties, tradeIns));
    }

    // Managers choose an assignee from the active agents of the sale's own branch; agents never list colleagues.
    public async Task<SaleReadResult<IReadOnlyList<SaleAgent>>> AgentsAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken)
    {
        var (status, actor, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
        if (sale is null)
        {
            return new(status);
        }

        if (!SaleAccess.IsManager(actor!, sale.BranchId))
        {
            return new(SaleResultStatus.Forbidden);
        }

        var agents = await database.Users.AsNoTracking().Where(item =>
                item.Role == AccountRoles.SalesAgent && item.BranchId == sale.BranchId && item.IsActive)
            .OrderBy(item => item.Email).ThenBy(item => item.Id)
            .Select(item => new SaleAgent(item.Id, item.Email ?? string.Empty,
                database.StaffProfiles.Where(profile => profile.UserId == item.Id).Select(profile => profile.Name ?? PersonLabels.FormerEmployee)
                    .FirstOrDefault() ?? string.Empty)).ToListAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, agents);
    }

    // Newest first, paged in SQL; the total and the rows come from one snapshot.
    public async Task<SaleReadResult<SaleHistoryPage>> HistoryAsync(Guid actorId, Guid saleId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,
            cancellationToken);
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
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
        var names = await NamesAsync(rows.SelectMany(row => new[] { row.ActorId, row.AgentId, row.PreviousAgentId }),
            cancellationToken);
        var operationIds = rows.Select(row => row.Id).ToArray();
        var documents = await database.SaleDocuments.AsNoTracking()
            .Where(item => item.SalesOrderId == saleId && operationIds.Contains(item.IssuingOperationId) &&
                item.CorrectionOperationId == null)
            .Select(item => new { item.Id, item.IssuingOperationId, item.Kind, item.Number })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new SaleHistoryItem(row.Id, row.Sequence, row.Kind.ToString(), row.RecordedAt,
            Name(names, row.ActorId) ?? "System", Name(names, row.AgentId), Name(names, row.PreviousAgentId),
            row.FromStatus?.ToString(), row.ToStatus.ToString(), row.Reason, row.Changes ?? [], row.Refund)
        {
            ActorId = row.ActorId,
            Documents = [.. documents.Where(item => item.IssuingOperationId == row.Id).OrderBy(item => item.Kind)
                .Select(item => new SaleHistoryDocument(item.Id, item.Kind.ToString(), item.Number))]
        }).ToList();
        await transaction.CommitAsync(cancellationToken);
        return new(SaleResultStatus.Succeeded, new SaleHistoryPage(items, total, page, pageSize));
    }

    public async Task<SaleReadResult<SaleVersionView>> VersionAsync(Guid actorId, Guid saleId, long version,
        CancellationToken cancellationToken)
    {
        var (status, _, sale) = await SaleReadScope.ReadAsync(database, actorId, saleId, cancellationToken);
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
            return new(SaleResultStatus.NotFound);
        }

        var previous = entries.SingleOrDefault(item => item.Sequence == version - 1);
        var names = await NamesAsync([entry.ActorId, entry.AgentId], cancellationToken);
        return new(SaleResultStatus.Succeeded, new SaleVersionView(saleId, entry.Sequence, entry.Kind.ToString(),
            entry.RecordedAt, Name(names, entry.ActorId) ?? "System", Name(names, entry.AgentId), entry.Reason,
            entry.Snapshot, previous?.Snapshot));
    }

    private Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken) =>
        AccountNames.ReadAsync(database, ids, cancellationToken);

    private static string? Name(Dictionary<Guid, string> names, Guid? id) =>
        id is { } value ? names.GetValueOrDefault(value, string.Empty) : null;
}
