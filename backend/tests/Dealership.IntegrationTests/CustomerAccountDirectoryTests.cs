using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;

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

        var page = (await admin.GetFromJsonAsync<CustomerAccountPage>(
            $"/api/customer-accounts?search={Uri.EscapeDataString(registration.Email!)}"))!;
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

        var detail = (await admin.GetFromJsonAsync<CustomerAccountDetail>($"/api/customer-accounts/{row.Id}"))!;
        Assert.Equal((registration.Email!, "0123456789", registration.ResidentialAddress), (detail.Email, detail.Mobile, detail.Address));
        Assert.Equal("********" + registration.Nric![^4..], detail.Nric);
        Assert.DoesNotContain(registration.Nric!, await admin.GetStringAsync($"/api/customer-accounts/{row.Id}"));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/customer-accounts/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/customer-accounts?tab=Staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/customer-accounts")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/customer-accounts")).StatusCode);
    }
}
