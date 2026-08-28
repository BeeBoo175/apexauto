using Dealership.Api.Configuration;
using Dealership.Domain.Inspections;

namespace Dealership.Api.Endpoints;

public static class InspectionGuideEndpoints
{
    public static void MapInspectionGuideEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/inspection-guide").NoStore().MapGet("", Read);
    }

    private static IResult Read()
    {
        return Results.Ok(new
        {
            catalogVersion = InspectionCatalog.Version,
            points = InspectionCatalog.Points,
            grading = new
            {
                gradeBMinimumRepairCost = InspectionGrading.GradeBMinimumRepairCost,
                gradeCMinimumRepairCost = InspectionGrading.GradeCMinimumRepairCost,
                maximumRepairCost = InspectionGrading.MaximumRepairCost
            }
        });
    }
}
