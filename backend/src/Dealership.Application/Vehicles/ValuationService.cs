using Dealership.Domain.Common;
using Dealership.Domain.Valuation;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Vehicles;

public sealed class ValuationService(TimeProvider timeProvider)
{

    public ValuationResult Calculate(ValuationRequest request)
    {
        var currentYear = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), DealershipLocale.Malaysia).Year;
        var errors = new Dictionary<string, string[]>();
        var make = VehicleTaxonomy.Makes.FirstOrDefault(item => item.Name == request.Make);
        var model = make?.Models.FirstOrDefault(item => item.Name == request.Model);
        var trim = model?.Trims.FirstOrDefault(item => item.Name == request.Trim);

        if (trim is null)
        {
            errors["vehicle"] = ["Select a make, model and trim from the supported taxonomy."];
        }

        if (request.ManufactureYear is null || request.ManufactureYear <= 0 || request.ManufactureYear > currentYear)
        {
            errors["manufactureYear"] = ["Enter a positive manufacture year no later than the current Malaysian year."];
        }

        if (request.MileageKilometres is null || request.MileageKilometres < 0)
        {
            errors["mileageKilometres"] = ["Enter a non-negative whole-number mileage in kilometres."];
        }

        if (errors.Count > 0)
        {
            return new ValuationResult(null, errors);
        }

        var quote = AlgorithmicValuationEngine.Calculate(trim!.BaselinePrice, request.ManufactureYear!.Value, request.MileageKilometres!.Value, currentYear);
        var response = new ValuationResponse(quote.MinValuation.Amount, quote.MaxValuation.Amount, DealershipLocale.DefaultCurrency, currentYear);

        return new ValuationResult(response, errors);
    }
}
