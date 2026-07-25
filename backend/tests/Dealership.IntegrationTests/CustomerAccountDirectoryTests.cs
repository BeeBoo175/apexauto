using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dealership.IntegrationTests;

public sealed class CustomerAccountDirectoryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AdministratorsFindCustomersByMaskedRowsAndReadTheirAccountWithTheNricMasked()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        using var customer = factory.CreateClient();
        var registration = await IntakePhotoFixture.RegisterAsync(customer);

        using var listResponse = await admin.GetAsync(
            $"/api/customer-accounts?search={Uri.EscapeDataString(registration.Email!)}");
        Assert.True(listResponse.Headers.CacheControl?.NoStore);
        var page = (await listResponse.Content.ReadFromJsonAsync<CustomerAccountPage>())!;
        var row = Assert.Single(page.Items);
        Assert.Equal(registration.LegalName, row.Name);
        Assert.NotEqual(registration.Email!, row.Email);
        Assert.StartsWith(registration.Email![0].ToString(), row.Email, StringComparison.Ordinal);
        Assert.EndsWith("@example.invalid", row.Email, StringComparison.Ordinal);
        Assert.Equal("******6789", row.Mobile);
        Assert.False(row.Verified);
        Assert.Contains(page.Items, item => item.Id == row.Id);
        Assert.True(page.Counts.Unverified >= 1);
        Assert.Contains((await admin.GetFromJsonAsync<CustomerAccountPage>("/api/customer-accounts?tab=Unverified"))!.Items,
            item => item.Id == row.Id);
        Assert.Empty((await admin.GetFromJsonAsync<CustomerAccountPage>("/api/customer-accounts?tab=Suspended&search=" +
            Uri.EscapeDataString(registration.Email!)))!.Items);

        using var detailResponse = await admin.GetAsync($"/api/customer-accounts/{row.Id}");
        Assert.True(detailResponse.Headers.CacheControl?.NoStore);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<CustomerAccountDetail>())!;
        Assert.Equal((registration.Email!, "0123456789", registration.ResidentialAddress), (detail.Email, detail.Mobile, detail.Address));
        Assert.Equal("********" + registration.Nric![^4..], detail.Nric);
        Assert.DoesNotContain(registration.Nric!, await admin.GetStringAsync($"/api/customer-accounts/{row.Id}"));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/customer-accounts/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/customer-accounts?tab=Staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/customer-accounts")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/customer-accounts")).StatusCode);

        using var unavailableFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICustomerAccountDirectory>();
            services.AddSingleton<ICustomerAccountDirectory, CancelledDirectory>();
            services.RemoveAll<IPersonQuery>();
            services.AddSingleton<IPersonQuery, CancelledPeople>();
        }));
        using var unavailable = unavailableFactory.CreateClient();
        unavailable.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        var requests = new[]
        {
            (Path: "/api/customer-accounts", Title: "Customer accounts are temporarily unavailable."),
            (Path: $"/api/customer-accounts/{row.Id}", Title: "Customer account details are temporarily unavailable."),
            (Path: $"/api/people/{row.Id}", Title: "Person details are temporarily unavailable.")
        };

        foreach (var request in requests)
        {
            using var response = await unavailable.GetAsync(request.Path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(request.Title, problem?.Title);
        }
    }

    private sealed class CancelledDirectory : ICustomerAccountDirectory
    {
        public Task<CustomerDirectoryResult<CustomerAccountPage>> ListAsync(Guid administratorId,
            CustomerDirectoryQuery query, CancellationToken cancellationToken)
        {
            return Task.FromException<CustomerDirectoryResult<CustomerAccountPage>>(new OperationCanceledException());
        }

        public Task<CustomerDirectoryResult<CustomerAccountDetail>> GetAsync(Guid administratorId, Guid customerId,
            CancellationToken cancellationToken)
        {
            return Task.FromException<CustomerDirectoryResult<CustomerAccountDetail>>(new OperationCanceledException());
        }
    }

    private sealed class CancelledPeople : IPersonQuery
    {
        public Task<Person?> FindAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromException<Person?>(new OperationCanceledException());
        }
    }
}
