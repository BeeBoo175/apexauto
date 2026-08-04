using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PromoAdministrationStore(DealershipDbContext database) : IPromoAdministrationStore
{
    public async Task<PromoAdministrationResult> ListAsync(Guid actorId, AdministrationListQuery query,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(PromoAdministrationStatus.Forbidden);
        }

        var promos = Filter(database.PromoCodes.AsNoTracking(), query, now);
        var total = await promos.CountAsync(cancellationToken);
        // Code is unique, so every ordering ends in a total tie-breaker and paging never repeats or skips a promo.
        var items = await Order(promos, query, now).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var result = new PromoPage([.. items.Select(PromoView.From)], total, query.Page, query.PageSize);
        return new(PromoAdministrationStatus.Succeeded, Page: result);
    }

    public async Task<PromoAdministrationResult> GetAsync(Guid actorId, Guid promoId,
        CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(PromoAdministrationStatus.Forbidden);
        }

        var promo = await database.PromoCodes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == promoId,
            cancellationToken);
        return promo is null ? new(PromoAdministrationStatus.NotFound)
            : new(PromoAdministrationStatus.Succeeded, PromoView.From(promo));
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

    // Expired takes precedence over the active flag so the three states stay mutually exclusive.
    private static IQueryable<PromoCode> Filter(IQueryable<PromoCode> promos, AdministrationListQuery query,
        DateTimeOffset now)
    {
        if (query.Search is not null)
        {
            var pattern = LiteralSearch.Pattern(query.Search);
            promos = promos.Where(item => EF.Functions.ILike(item.Code, pattern, "!"));
        }

        if (query.Text is not null)
        {
            var pattern = LiteralSearch.Pattern(query.Text);
            promos = promos.Where(item => EF.Functions.ILike(item.Code, pattern, "!"));
        }

        if (query.MinimumAmount is { } minimum)
        {
            promos = promos.Where(item => item.Discount >= minimum);
        }

        if (query.MaximumAmount is { } maximum)
        {
            promos = promos.Where(item => item.Discount <= maximum);
        }

        if (query.Dates is { From: { } from, Until: { } until })
        {
            promos = promos.Where(item => item.ExpiresAtUtc >= from && item.ExpiresAtUtc < until);
        }

        return query.Status switch
        {
            "active" => promos.Where(item => item.IsActive && item.ExpiresAtUtc > now),
            "inactive" => promos.Where(item => !item.IsActive && item.ExpiresAtUtc > now),
            "expired" => promos.Where(item => item.ExpiresAtUtc <= now),
            _ => promos
        };
    }

    private static IOrderedQueryable<PromoCode> Order(IQueryable<PromoCode> promos, AdministrationListQuery query,
        DateTimeOffset now)
    {
        var descending = query.Descending;
        return query.Sort switch
        {
            "code" => descending ? promos.OrderByDescending(item => item.Code) : promos.OrderBy(item => item.Code),
            "discount" => (descending ? promos.OrderByDescending(item => item.Discount) : promos.OrderBy(item => item.Discount))
                .ThenBy(item => item.Code),
            "expiresAt" => (descending ? promos.OrderByDescending(item => item.ExpiresAtUtc) : promos.OrderBy(item => item.ExpiresAtUtc))
                .ThenBy(item => item.Code),
            "status" => (descending ? promos.OrderByDescending(item => item.ExpiresAtUtc <= now ? 2 : item.IsActive ? 0 : 1)
                : promos.OrderBy(item => item.ExpiresAtUtc <= now ? 2 : item.IsActive ? 0 : 1)).ThenBy(item => item.Code),
            _ => promos.OrderByDescending(item => item.ExpiresAtUtc).ThenBy(item => item.Code)
        };
    }

    // PostgreSQL stores microseconds, so a replayed expiry is compared at the stored precision.
    private static bool Matches(PromoCode existing, PromoCreateRequest requested) =>
        existing.Id == requested.Id && existing.Code == requested.Code && existing.Discount == requested.Discount &&
        existing.ExpiresAtUtc == requested.ExpiresAtUtc.AddTicks(-requested.ExpiresAtUtc.Ticks % 10) &&
        existing.IsActive == requested.IsActive;
}
