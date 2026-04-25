using System.Text.Json;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Photos;

namespace Dealership.UnitTests;

public sealed class DevelopmentVehicleCatalogueTests
{
    [Fact]
    public async Task EveryFixtureUsesSupportedTaxonomyAndDecodableAttributedModelPhotos()
    {
        var assembly = typeof(DevelopmentFixturePhotos).Assembly;
        using var attribution = assembly.GetManifestResourceStream("Dealership.Infrastructure.Development.Media.attribution.json");
        Assert.NotNull(attribution);
        using var manifest = await JsonDocument.ParseAsync(attribution);
        using var validator = new PhotoImageValidator();
        var vehicles = DevelopmentIntakeManifest.Appointments.Select(item => item.Vehicle).DistinctBy(item =>
            (item.Make, item.Model, item.Trim)).ToArray();

        foreach (var vehicle in vehicles)
        {
            var make = Assert.Single(VehicleTaxonomy.Makes, item => item.Name == vehicle.Make);
            var model = Assert.Single(make.Models, item => item.Name == vehicle.Model);
            Assert.Contains(model.Trims, item => item.Name == vehicle.Trim);
            var details = DevelopmentVehicleCatalogue.For(make.Name, model.Name, vehicle.Trim);
            foreach (var angle in Enum.GetValues<PhotoAngle>())
            {
                using var photo = DevelopmentFixturePhotos.Open(angle, details);
                var validated = await validator.ValidateAsync(photo, details.ContentType(angle), CancellationToken.None);
                Assert.NotNull(validated);
                var source = Assert.Single(manifest.RootElement.EnumerateArray(), item =>
                    item.GetProperty("file").GetString() == details.PhotoFile(angle));
                Assert.Equal(source.GetProperty("sha256").GetString()!.ToLowerInvariant(), validated.Sha256);
                Assert.StartsWith("https://commons.wikimedia.org/", source.GetProperty("source").GetString());
                Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("license").GetString()));
            }
        }
    }
}
