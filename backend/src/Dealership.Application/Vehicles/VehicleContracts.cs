namespace Dealership.Application.Vehicles;

public sealed record ValuationRequest(string? Make, string? Model, string? Trim, int? ManufactureYear, int? MileageKilometres);

public sealed record ValuationResponse(decimal MinValuation, decimal MaxValuation, string Currency, int ValuationYear);

public sealed record ValuationResult(ValuationResponse? Quote, IReadOnlyDictionary<string, string[]> Errors);
