using Dealership.Application.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class PromoAdministrationService(IPromoAdministrationStore store)
{
    public Task<PromoAdministrationResult> ListAsync(Guid actorId, int page, int pageSize,
        CancellationToken cancellationToken) =>
        actorId == Guid.Empty ? Rejected(PromoAdministrationStatus.Forbidden)
            : !Pagination.IsValid(page, pageSize) ? Rejected(PromoAdministrationStatus.Invalid)
            : store.ListAsync(actorId, page, pageSize, cancellationToken);

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
