using Dealership.Domain.Inspections;

namespace Dealership.UnitTests;

public class InspectionCatalogTests
{
    [Theory]
    [InlineData(InspectionCategory.FrameChassis, 45)]
    [InlineData(InspectionCategory.EngineTransmission, 50)]
    [InlineData(InspectionCategory.ExteriorPaint, 45)]
    [InlineData(InspectionCategory.InteriorElectrical, 40)]
    [InlineData(InspectionCategory.UnderbodyBrakesSuspension, 30)]
    public void RetainsApprovedCategoryVolumes(InspectionCategory category, int expected)
    {
        Assert.Equal(expected, InspectionCatalog.Points.Count(point => point.Category == category));
    }

    [Fact]
    public void RetainsVersionedUniqueDefinitionsAndProtectsSharedCollection()
    {
        Assert.Equal("apex-inspection-1", InspectionCatalog.Version);
        Assert.Equal(210, InspectionCatalog.Points.Count);
        Assert.Equal(210, InspectionCatalog.Points.Select(point => point.Code).Distinct().Count());
        Assert.All(InspectionCatalog.Points, point =>
        {
            Assert.False(string.IsNullOrWhiteSpace(point.Description));
            Assert.Same(point, InspectionCatalog.GetPoint(point.Code));
        });

        var collection = Assert.IsAssignableFrom<IList<InspectionPoint>>(InspectionCatalog.Points);
        Assert.Throws<NotSupportedException>(() => collection.RemoveAt(0));
    }
}
