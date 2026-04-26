using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PromoCreateRequest(Guid Id, string Code, decimal Discount, DateTimeOffset ExpiresAtUtc,
    bool IsActive = true);

public sealed record PromoUpdateRequest(decimal Discount, DateTimeOffset ExpiresAtUtc, bool IsActive,
    long ExpectedVersion);

public enum PromoAdministrationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record PromoView(Guid Id, string Code, decimal Discount, DateTimeOffset ExpiresAtUtc, bool IsActive,
    long Version)
{
    public static PromoView From(PromoCode promo) => new(promo.Id, promo.Code, promo.Discount, promo.ExpiresAtUtc,
        promo.IsActive, promo.Version);
}

public sealed record PromoPage(IReadOnlyList<PromoView> Items, int Total, int Page, int PageSize);

// Conflict carries the current promo so a stale editor can reload without another read.
public sealed record PromoAdministrationResult(PromoAdministrationStatus Status, PromoView? Promo = null,
    PromoPage? Page = null);

public interface IPromoAdministrationStore
{
    Task<PromoAdministrationResult> ListAsync(Guid actorId, AdministrationListQuery query, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<PromoAdministrationResult> CreateAsync(Guid actorId, PromoCreateRequest request,
        CancellationToken cancellationToken);

    Task<PromoAdministrationResult> UpdateAsync(Guid actorId, Guid promoId, PromoUpdateRequest request,
        CancellationToken cancellationToken);
}
