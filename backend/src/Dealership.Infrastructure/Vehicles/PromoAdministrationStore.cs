using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PromoAdministrationStore(DealershipDbContext database) : IPromoAdministrationStore
{
    public async Task<PromoAdministrationResult> ListAsync(Guid actorId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(PromoAdministrationStatus.Forbidden);
        }

        var promos = database.PromoCodes.AsNoTracking();
        var total = await promos.CountAsync(cancellationToken);
        // Code is unique, so the ordering is total and paging never repeats or skips a promo.
        var items = await promos.OrderByDescending(item => item.ExpiresAtUtc).ThenBy(item => item.Code)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var result = new PromoPage(items.Select(PromoView.From).ToList(), total, page, pageSize);
        return new(PromoAdministrationStatus.Succeeded, Page: result);
    }

    public async Task<PromoAdministrationResult> CreateAsync(Guid actorId, PromoCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(PromoAdministrationStatus.Forbidden);
        }

        // Serialize the identity and code pair so a replay or a competing writer sees one outcome.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"purchase-configuration"}, 0))", cancellationToken);
        var existing = await database.PromoCodes.AsNoTracking()
            .Where(item => item.Id == request.Id || item.Code == request.Code).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            // An identical replay reconciles a lost receipt; any other overlap is a conflict.
            var replay = existing.Count == 1 && Matches(existing[0], request);
            return replay ? new(PromoAdministrationStatus.Succeeded, PromoView.From(existing[0]))
                : new(PromoAdministrationStatus.Conflict);
        }

        var promo = new PromoCode(request.Id, request.Code, request.Discount, request.ExpiresAtUtc, request.IsActive);
        database.PromoCodes.Add(promo);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PromoAdministrationStatus.Succeeded, PromoView.From(promo));
    }

    public async Task<PromoAdministrationResult> UpdateAsync(Guid actorId, Guid promoId, PromoUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(PromoAdministrationStatus.Forbidden);
        }

        var promo = await database.PromoCodes.FromSqlInterpolated(
            $"SELECT * FROM \"PromoCodes\" WHERE \"Id\" = {promoId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (promo is null)
        {
            return new(PromoAdministrationStatus.NotFound);
        }

        if (promo.Version != request.ExpectedVersion)
        {
            return new(PromoAdministrationStatus.Conflict, PromoView.From(promo));
        }

        promo.Update(request.Discount, request.ExpiresAtUtc, request.IsActive);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PromoAdministrationStatus.Succeeded, PromoView.From(promo));
    }

    // PostgreSQL stores microseconds, so a replayed expiry is compared at the stored precision.
    private static bool Matches(PromoCode existing, PromoCreateRequest requested) =>
        existing.Id == requested.Id && existing.Code == requested.Code && existing.Discount == requested.Discount &&
        existing.ExpiresAtUtc == requested.ExpiresAtUtc.AddTicks(-requested.ExpiresAtUtc.Ticks % 10) &&
        existing.IsActive == requested.IsActive;
}
