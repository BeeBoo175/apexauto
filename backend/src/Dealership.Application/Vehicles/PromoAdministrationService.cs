using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class PromoAdministrationService(IPromoAdministrationStore store, TimeProvider clock)
{
    public static readonly IReadOnlySet<string> Statuses =
        new HashSet<string> { AdministrationListQuery.AllStatuses, "active", "inactive", "expired" };

    public static readonly IReadOnlySet<string> Sorts =
        new HashSet<string> { "code", "discount", "expiresAt", "status" };

    public Task<PromoAdministrationResult> ListAsync(Guid actorId, int page, int pageSize, string? search,
        string? status, string? sort, string? direction, string? code, decimal? minimumDiscount,
        decimal? maximumDiscount, string? expiresFrom, string? expiresTo, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(PromoAdministrationStatus.Forbidden);
        }

        var query = AdministrationListQuery.Create(page, pageSize, search, status, sort, direction, Statuses, Sorts)
            ?.WithColumns(code, minimumDiscount, maximumDiscount, expiresFrom, expiresTo, null);
        return query is null ? Rejected(PromoAdministrationStatus.Invalid)
            : store.ListAsync(actorId, query, clock.GetUtcNow(), cancellationToken);
    }

    public Task<PromoAdministrationResult> GetAsync(Guid actorId, Guid promoId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty ? Rejected(PromoAdministrationStatus.Forbidden)
            : promoId == Guid.Empty ? Rejected(PromoAdministrationStatus.Invalid)
            : store.GetAsync(actorId, promoId, cancellationToken);

    public Task<PromoAdministrationResult> CreateAsync(Guid actorId, PromoCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(PromoAdministrationStatus.Forbidden);
        }

        if (request.Id == Guid.Empty || !PromoCode.ValidCode(request.Code) || !PromoCode.ValidDiscount(request.Discount) ||
            !FiniteUtc(request.ExpiresAtUtc))
        {
            return Rejected(PromoAdministrationStatus.Invalid);
        }

        return store.CreateAsync(actorId, request with { Code = request.Code.Trim() }, cancellationToken);
    }

    public Task<PromoAdministrationResult> UpdateAsync(Guid actorId, Guid promoId, PromoUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Rejected(PromoAdministrationStatus.Forbidden);
        }

        if (promoId == Guid.Empty || request.ExpectedVersion <= 0 || !PromoCode.ValidDiscount(request.Discount) ||
            !FiniteUtc(request.ExpiresAtUtc))
        {
            return Rejected(PromoAdministrationStatus.Invalid);
        }

        return store.UpdateAsync(actorId, promoId, request, cancellationToken);
    }

    private static bool FiniteUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero &&
        value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;

    private static Task<PromoAdministrationResult> Rejected(PromoAdministrationStatus status) =>
        Task.FromResult(new PromoAdministrationResult(status));
}
