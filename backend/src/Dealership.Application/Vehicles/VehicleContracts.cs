namespace Dealership.Application.Vehicles;

public sealed record TaxonomyMake(string Name, IReadOnlyList<TaxonomyModel> Models);

public sealed record TaxonomyModel(string Name, IReadOnlyList<TaxonomyTrim> Trims);

public sealed record TaxonomyTrim(string Name, decimal BaselinePrice);

public sealed record ValuationRequest(string? Make, string? Model, string? Trim, int? ManufactureYear, int? MileageKilometres);

public sealed record ValuationResponse(decimal MinValuation, decimal MaxValuation, string Currency, int ValuationYear);

public sealed record ValuationResult(ValuationResponse? Quote, IReadOnlyDictionary<string, string[]> Errors);
