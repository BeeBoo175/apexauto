using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed record PurchaseSettingsUpdateRequest(decimal ProcessingFee, decimal SstRate,
    decimal MaxAgentDiscountAmount, int ReservationHoldHours, long ExpectedVersion);

public enum PurchaseSettingsAdministrationStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict
}

public sealed record PurchaseSettingsView(decimal ProcessingFee, decimal SstRate, decimal MaxAgentDiscountAmount,
    int ReservationHoldHours, DateTimeOffset UpdatedAtUtc, long Version)
{
    public static PurchaseSettingsView From(PurchaseSettings settings) => new(settings.ProcessingFee,
        settings.SstRate, settings.MaxAgentDiscountAmount, settings.ReservationHoldHours, settings.UpdatedAtUtc,
        settings.Version);
}

public sealed record PurchaseSettingsAdministrationResult(PurchaseSettingsAdministrationStatus Status,
    PurchaseSettingsView? Settings = null);

public interface IPurchaseSettingsAdministrationStore
{
    Task<PurchaseSettingsAdministrationResult> ReadAsync(Guid actorId, CancellationToken cancellationToken);

    Task<PurchaseSettingsAdministrationResult> UpdateAsync(Guid actorId, PurchaseSettingsUpdateRequest request,
        DateTimeOffset now, CancellationToken cancellationToken);
}
