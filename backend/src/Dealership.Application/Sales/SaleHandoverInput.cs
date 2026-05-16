using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Nullable numeric inputs distinguish an omitted physical count from an explicitly confirmed zero.
public sealed record SaleHandoverInput(Guid OperationId, long ExpectedVersion, SaleHandoverEvidenceInput? Evidence)
{
    public SaleHandoverRequest? Command() => Evidence is { OdometerKilometres: { } odometer, KeysDelivered: { } keys }
        ? new(OperationId, ExpectedVersion, new SaleHandoverEvidence(odometer, keys, Evidence.CustomerAcknowledged,
            Evidence.OwnershipTransferCompleted, Evidence.InsuranceConfirmed))
        : null;
}

public sealed record SaleHandoverEvidenceInput(int? OdometerKilometres, int? KeysDelivered,
    bool CustomerAcknowledged, bool OwnershipTransferCompleted, bool InsuranceConfirmed);
