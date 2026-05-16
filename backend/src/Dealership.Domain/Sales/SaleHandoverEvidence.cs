namespace Dealership.Domain.Sales;

// Physical delivery confirmations, separate from the already settled commercial evidence.
public sealed record SaleHandoverEvidence(int OdometerKilometres, int KeysDelivered,
    bool CustomerAcknowledged, bool OwnershipTransferCompleted, bool InsuranceConfirmed)
{
    public SaleHandoverEvidence Validate(int recordedMileage)
    {
        if (OdometerKilometres < recordedMileage || OdometerKilometres < 0 || KeysDelivered < 0 ||
            !CustomerAcknowledged || !OwnershipTransferCompleted || !InsuranceConfirmed)
        {
            throw new ArgumentException("Handover requires a nondecreasing odometer, key count and all delivery confirmations.");
        }

        return this;
    }
}
