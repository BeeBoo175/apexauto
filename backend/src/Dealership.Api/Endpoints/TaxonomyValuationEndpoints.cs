using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Common;
using Dealership.Domain.Valuation;

namespace Dealership.Api.Endpoints;

public static class TaxonomyValuationEndpoints
{
    public static void MapTaxonomyValuationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/taxonomy", GetTaxonomy);
        app.MapPost("/api/valuations", CalculateValuation);
    }

    private static IResult GetTaxonomy()
    {
        return Results.Ok(new
        {
            currency = DealershipLocale.DefaultCurrency,
            maximumMileageKilometres = ValuationPolicy.MaximumMileageKilometres,
            makes = VehicleTaxonomy.Makes
        });
    }

    private static IResult CalculateValuation(ValuationRequest request, ValuationService service)
    {
        var result = service.Calculate(request);

        if (result.Quote is null)
        {
            return Results.ValidationProblem(result.Errors.ToDictionary());
        }

        return Results.Ok(result.Quote);
    }
}
