using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dealership.Application.Vehicles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

public class VehicleEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public VehicleEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            });
        });

        _client = _factory.CreateClient();
    }

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

    [Theory]
    [InlineData("Proton", "Myvi", "1.5 AV", 2025, 15000, "vehicle")]
    [InlineData("Perodua", "Myvi", "Flagship", 2025, 15000, "vehicle")]
    [InlineData(null, null, null, 2025, 15000, "vehicle")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2027, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 0, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", null, 15000, "manufactureYear")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2025, -1, "mileageKilometres")]
    [InlineData("Perodua", "Myvi", "1.5 AV", 2025, null, "mileageKilometres")]
    public async Task InvalidRequestsReturnFieldErrors(string? make, string? model, string? trim, int? year, int? mileage, string field)
    {
        var response = await _client.PostAsJsonAsync("/api/valuations", new ValuationRequest(make, model, trim, year, mileage));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"manufactureYear\":\"2025\"}")]
    [InlineData("{\"mileageKilometres\":1.5}")]
    [InlineData("{\"mileageKilometres\":2147483648}")]
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
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2025, 12, 31, 16, 0, 0, TimeSpan.Zero);
        }
    }
}
