using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class PurchaseSettingsAdministrationService(IPurchaseSettingsAdministrationStore store, TimeProvider clock)
{
    public Task<PurchaseSettingsAdministrationResult> ReadAsync(Guid actorId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty
            ? Task.FromResult(new PurchaseSettingsAdministrationResult(PurchaseSettingsAdministrationStatus.Forbidden))
            : store.ReadAsync(actorId, clock.GetUtcNow(), cancellationToken);

    public Task<PurchaseSettingsAdministrationResult> UpdateAsync(Guid actorId, PurchaseSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Task.FromResult(new PurchaseSettingsAdministrationResult(PurchaseSettingsAdministrationStatus.Forbidden));
        }

        if (request.ExpectedVersion <= 0 || !PurchaseSettings.ValidValues(request.ProcessingFee, request.SstRate,
            request.MaxAgentDiscountAmount, request.ReservationHoldHours))
        {
            return Task.FromResult(new PurchaseSettingsAdministrationResult(PurchaseSettingsAdministrationStatus.Invalid));
        }

        return store.UpdateAsync(actorId, request, clock.GetUtcNow(), cancellationToken);
    }
}
