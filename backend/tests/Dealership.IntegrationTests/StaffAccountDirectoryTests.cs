using System.Net;
using System.Net.Http.Json;
using Dealership.Application.Accounts;

namespace Dealership.IntegrationTests;

public sealed class StaffAccountDirectoryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AdministratorsListStaffAndInvitationsByTabSearchAndFilterWhileOthersAreRefused()
    {
        using var factory = fixture.CreateFactory();
        using var admin = factory.CreateClient();
        var branchId = await StaffInvitationTests.SignInAdministratorAsync(factory, admin);
        var invitationId = Guid.NewGuid();
        var email = $"pending.{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsJsonAsync("/api/staff-invitations", new
        {
            id = invitationId,
            email,
            role = AccountRoles.SalesAgent,
            branchId,
            name = "Zahra Invited",
            mobile = "0129876543"
        })).StatusCode);

        var all = (await admin.GetFromJsonAsync<StaffAccountPage>("/api/staff-accounts?pageSize=25"))!;
        Assert.Equal(all.Total, all.Counts.All);
        Assert.Equal(all.Counts.Active + all.Counts.Invited + all.Counts.Deactivated, all.Counts.All);
        Assert.Contains(all.Items, row => row.Id == invitationId && row.Status == StaffAccountStatuses.Invited && row.Email == email);
        Assert.Contains(all.Items, row => row.Role == AccountRoles.Administrator && row.Status == StaffAccountStatuses.Active);

        var invited = (await admin.GetFromJsonAsync<StaffAccountPage>($"/api/staff-accounts?tab=Invited&search={Uri.EscapeDataString("zahra")}"))!;
        Assert.Equal(invitationId, Assert.Single(invited.Items).Id);
        var byBranch = (await admin.GetFromJsonAsync<StaffAccountPage>($"/api/staff-accounts?branchId={branchId}&role={AccountRoles.SalesAgent}"))!;
        Assert.All(byBranch.Items, row => Assert.Equal(branchId, row.BranchId));
        Assert.Empty((await admin.GetFromJsonAsync<StaffAccountPage>("/api/staff-accounts?search=%25"))!.Items);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/staff-accounts?tab=Suspended")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/staff-accounts?role={AccountRoles.Customer}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/staff-accounts?search={new string('x', 161)}")).StatusCode);

        using var revoked = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/staff-invitations/{invitationId}/revocations", null)).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<StaffAccountPage>("/api/staff-accounts"))!.Items,
            row => row.Id == invitationId);

        using var customer = factory.CreateClient();
        await IntakePhotoFixture.RegisterAsync(customer);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/staff-accounts")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/staff-accounts")).StatusCode);
    }
}
