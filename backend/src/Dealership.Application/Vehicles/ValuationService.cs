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

        var year = request.ManufactureYear;
        var yearOutsideTrim = trim is not null && year is not null && !trim.AcceptsYear(year.Value, currentYear);
        if (year is null or <= 0 || year > currentYear || yearOutsideTrim)
        {
            errors["manufactureYear"] = trim is null
                ? ["Enter a positive manufacture year no later than the current Malaysian year."]
                : [$"Choose a manufacture year from {trim.FirstYear} to the current Malaysian year for this trim."];
        }

        if (request.MileageKilometres is null || request.MileageKilometres < 0 ||
            request.MileageKilometres > ValuationPolicy.MaximumMileageKilometres)
        {
            errors["mileageKilometres"] =
                [$"Enter a whole-number mileage from 0 to {ValuationPolicy.MaximumMileageKilometres} kilometres."];
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
