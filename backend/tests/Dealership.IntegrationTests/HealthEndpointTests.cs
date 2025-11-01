using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Dealership.IntegrationTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealthReturnsOkWithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }
}
