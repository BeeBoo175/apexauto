namespace Dealership.Application.Vehicles;

public sealed record PurchaseGuideWarranty(Guid Id, string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade);

// The effective purchase terms the public buying guide explains: the fee and tax the quote applies and the active
// warranty packages, cheapest first.
public sealed record PurchaseGuide(decimal ProcessingFee, decimal SstRate,
    IReadOnlyList<PurchaseGuideWarranty> Warranties);

public interface IPurchaseGuideQuery
{
    Task<PurchaseGuide?> ReadAsync(CancellationToken cancellationToken);
}