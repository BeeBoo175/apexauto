using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Application.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

// Reuses the correction scenario's issued originals/replacements and host instead of provisioning another sale.
internal static class SaleDocumentPdfChecks
{
    public static async Task VerifyAsync(SaleWorld world, Guid saleId, Guid originalId, Guid replacementId,
        WebApplicationFactory<Program> factory, WebApplicationFactory<Program> restarted, string connection)
    {
        await using var database = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(connection).Options);
        var sale = await database.SalesOrders.AsNoTracking().SingleAsync(item => item.Id == saleId);
        var documents = world.Services.GetRequiredService<ISaleDocumentStore>();
        var issued = (await documents.ReadAsync(sale.CustomerId, saleId, default)).Value!;
        using var client = factory.CreateClient();
        var path = $"/api/sales/{saleId}/documents/{originalId}/pdf";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        await LoginAsync(client, database, sale.CustomerId);
        foreach (var document in issued.Append((await documents.ReadOneAsync(sale.CustomerId, saleId, originalId, default)).Value!))
        {
            var response = await client.GetAsync($"/api/sales/{saleId}/documents/{document.Id}/pdf");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("inline", response.Content.Headers.ContentDisposition!.DispositionType);
            Assert.Contains(document.Id.ToString("N"), response.Content.Headers.ContentDisposition.FileName);
            AssertPdf(await response.Content.ReadAsByteArrayAsync());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sales/{saleId}/documents/{Guid.NewGuid()}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sales/{Guid.NewGuid()}/documents/{originalId}/pdf")).StatusCode);
        using var scope = restarted.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SaleDocumentService>();
        foreach (var actor in new[] { world.Agent, world.Manager })
        {
            Assert.Equal(SaleResultStatus.Succeeded, (await service.ReadAsync(actor, saleId, originalId, default)).Status);
        }

        foreach (var actor in new[] { world.Colleague, world.OtherBranchAgent })
        {
            Assert.Equal(SaleResultStatus.NotFound, (await service.ReadAsync(actor, saleId, originalId, default)).Status);
        }

        var administrator = await ReservationTestData.ProvisionAsync(world.Services, null, AccountRoles.Administrator);
        Assert.Equal(SaleResultStatus.Forbidden, (await service.ReadAsync(administrator, saleId, originalId, default)).Status);
        var unrelated = await database.CustomerProfiles.Where(item => item.UserId != sale.CustomerId)
            .Select(item => item.UserId).FirstAsync();
        Assert.Equal(SaleResultStatus.NotFound, (await service.ReadAsync(unrelated, saleId, originalId, default)).Status);
        var before = (await documents.ReadOneAsync(sale.CustomerId, saleId, replacementId, default)).Value!;
        var profile = await database.CustomerProfiles.AsNoTracking().SingleAsync(item => item.UserId == sale.CustomerId);
        await database.CustomerProfiles.Where(item => item.UserId == sale.CustomerId).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.LegalName, "Nur Aina Abdul Rahman")
            .SetProperty(item => item.ResidentialAddress, "45 Jalan Kenanga, Shah Alam, Selangor"));
        try
        {
            Assert.Equal(before.Contents, (await documents.ReadOneAsync(sale.CustomerId, saleId, replacementId, default)).Value!.Contents);
            AssertPdf((await service.ReadAsync(sale.CustomerId, saleId, replacementId, default)).Value!);
            var capture = new CapturingRenderer(world.Services.GetRequiredService<ISaleDocumentRenderer>());
            AssertPdf((await new SaleDocumentService(documents, capture)
                .ReadAsync(sale.CustomerId, saleId, replacementId, default)).Value!);
            Assert.Equal(before, capture.Document);
        }
        finally
        {
            await database.CustomerProfiles.Where(item => item.UserId == sale.CustomerId).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.LegalName, profile.LegalName)
                .SetProperty(item => item.ResidentialAddress, profile.ResidentialAddress));
        }

        await database.Users.Where(item => item.Id == sale.CustomerId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, false));
        try
        {
            Assert.Equal(SaleResultStatus.Forbidden, (await service.ReadAsync(sale.CustomerId, saleId, originalId, default)).Status);
        }
        finally
        {
            await database.Users.Where(item => item.Id == sale.CustomerId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, true));
        }
    }

    private sealed class CapturingRenderer(ISaleDocumentRenderer renderer) : ISaleDocumentRenderer
    {
        public SaleIssuedDocument? Document { get; private set; }

        public byte[] Render(SaleIssuedDocument document)
        {
            Document = document;
            return renderer.Render(document);
        }
    }

    private static void AssertPdf(byte[] content)
    {
        Assert.True(content.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(content, 0, 5));
    }

    private static async Task LoginAsync(HttpClient client, DealershipDbContext database, Guid actorId)
    {
        var email = await database.Users.Where(item => item.Id == actorId).Select(item => item.Email).SingleAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email!, "Password123!"));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
