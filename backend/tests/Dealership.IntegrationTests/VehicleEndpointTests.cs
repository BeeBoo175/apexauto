using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Vehicles;

namespace Dealership.IntegrationTests;

public class VehicleEndpointTests(VehicleApiFactory factory) : IClassFixture<VehicleApiFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TaxonomyContainsAllSupportedChoicesWithPositiveDemoPrices()
    {
        var response = await _client.GetAsync("/api/taxonomy");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var makes = root.GetProperty("makes").Deserialize<TaxonomyMake[]>(JsonSerializerOptions.Web)!;

        Assert.Equal("MYR", root.GetProperty("currency").GetString());
        Assert.Equal(["Perodua", "Proton", "Honda", "Toyota", "Mazda", "BYD"], makes.Select(make => make.Name));
        Assert.Equal(24, makes.Sum(make => make.Models.Count));
        Assert.Equal(75, makes.SelectMany(make => make.Models).Sum(model => model.Trims.Count));

        foreach (var make in makes)
        {
            Assert.NotEmpty(make.Models);
            foreach (var model in make.Models)
            {
                Assert.NotEmpty(model.Trims);
                Assert.Equal(model.Trims.Count, model.Trims.Select(trim => trim.Name).Distinct().Count());
                Assert.All(model.Trims, trim => Assert.True(trim.BaselinePrice > 0m));
            }
        }
    }

    [Fact]
    public async Task QuoteUsesServerBaselineAndMalaysianYearWithoutAuthentication()
    {
        var request = new ValuationRequest("Perodua", "Myvi", "1.5 AV", 2025, 15000);
        var response = await _client.PostAsJsonAsync("/api/valuations", request);
        response.EnsureSuccessStatusCode();
        var quote = await response.Content.ReadFromJsonAsync<ValuationResponse>();

        Assert.NotNull(quote);
        Assert.Equal(48450m, quote.MinValuation);
        Assert.Equal(53550m, quote.MaxValuation);
        Assert.Equal(2026, quote.ValuationYear);
        Assert.Equal("MYR", quote.Currency);
    }

    [Fact]
    public async Task InvalidRequestMapsEveryFieldErrorToBadRequest()
    {
        var request = new ValuationRequest("Proton", "Myvi", "1.5 AV", 2027, -1);
        var response = await _client.PostAsJsonAsync("/api/valuations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("vehicle", out _));
        Assert.True(errors.TryGetProperty("manufactureYear", out _));
        Assert.True(errors.TryGetProperty("mileageKilometres", out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"make\":\"Perodua\",\"model\":\"Myvi\",\"trim\":\"1.5 AV\",\"manufactureYear\":\"not-a-year\",\"mileageKilometres\":15000}")]
    [InlineData("{\"make\":\"Perodua\",\"model\":\"Myvi\",\"trim\":\"1.5 AV\",\"manufactureYear\":2025,\"mileageKilometres\":1.5}")]
    [InlineData("{\"make\":\"Perodua\",\"model\":\"Myvi\",\"trim\":\"1.5 AV\",\"manufactureYear\":2025,\"mileageKilometres\":2147483648}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task MissingOrMalformedInputsCannotProduceAQuote(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/valuations", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonJsonBodyIsRejected()
    {
        using var content = new StringContent("not JSON", Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/valuations", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
