using System.Text.Json;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Photos;

namespace Dealership.UnitTests;

[Trait("Category", "SeedPipeline")]
public sealed class DevelopmentAccountPictureTests
{
    [Fact]
    public void AboutHalfOfTheNamedAccountsGetADistinctlyAssignedPicture()
    {
        var named = DevelopmentFixtureManifest.Customers.Count + DevelopmentFixtureManifest.Staff.Count;
        var pictures = DevelopmentFixtureManifest.Pictures;

        Assert.InRange(pictures.Count, named / 2, named / 2 + 2);
        Assert.Equal(pictures.Count, pictures.Select(item => item.Email).Distinct().Count());
        Assert.True(pictures.Select(item => item.File).Distinct().Count() > 1);
    }

    [Fact]
    public async Task EveryAssignedPictureIsDecodableAndCarriesSourceAuthorAndLicence()
    {
        var assembly = typeof(DevelopmentAccountPictureFixtures).Assembly;
        using var attribution = assembly.GetManifestResourceStream("Dealership.Infrastructure.Development.Media.attribution.json");
        Assert.NotNull(attribution);
        using var manifest = await JsonDocument.ParseAsync(attribution);
        using var validator = new PhotoImageValidator();

        foreach (var file in DevelopmentFixtureManifest.Pictures.Select(item => item.File).Distinct())
        {
            await using var content = assembly.GetManifestResourceStream($"Dealership.Infrastructure.Development.Media.{file}");
            Assert.NotNull(content);
            var validated = await validator.ValidateAsync(content, "image/jpeg", CancellationToken.None);
            Assert.NotNull(validated);
            var source = Assert.Single(manifest.RootElement.EnumerateArray(), item =>
                item.GetProperty("file").GetString() == file);
            Assert.Equal(source.GetProperty("sha256").GetString()!.ToLowerInvariant(), validated.Sha256);
            Assert.StartsWith("https://commons.wikimedia.org/", source.GetProperty("source").GetString());
            Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("author").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("license").GetString()));
        }
    }
}
