using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Common;

namespace Dealership.Api.Endpoints;

public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/taxonomy", GetTaxonomy);
        app.MapPost("/api/valuations", CalculateValuation);
    }

    private static IResult GetTaxonomy()
    {
        return Results.Ok(new
        {
            currency = DealershipLocale.DefaultCurrency,
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
