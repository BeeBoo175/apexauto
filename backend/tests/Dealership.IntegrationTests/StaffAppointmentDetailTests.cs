using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class StaffAppointmentDetailTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    private const string Route = "/api/staff/intake/appointments";
    private static readonly string[] DetailProperties =
    [
        "attendanceCorrectedAt", "branchId", "branchName", "correctedBy", "correctedByName", "correctionReason",
        "customerEmail", "customerMobile", "customerName", "customerSuspended", "endsAt", "id", "make", "manufactureYear", "mileageKilometres", "model", "noShowAt", "ownership", "registrationPlate",
        "startsAt", "status", "trim"
    ];

    [Fact]
    public async Task NormalAuthenticatedDetailPreservesBookedEvidenceStatusesAndCurrentStaffScopeAcrossRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = otherBranch, Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
        var booked = await data.BookAsync();
        await data.ClaimAsync(booked.Id);
        var cancellable = await data.BookAsync(0, "BPL 2198", dateOffset: 1);
        var other = await data.BookAsync(0, "VCA 6201", branchId: otherBranch);
        AuthenticationHeaderValue authorization;

        using (var factory = fixture.CreateFactory())
        using (var client = factory.CreateClient())
        {
            await AssertStatusAsync(client, booked.Id, HttpStatusCode.Unauthorized);
            await LoginAsync(client, data.StaffEmail);
            authorization = client.DefaultRequestHeaders.Authorization!;
            await AssertDetailAsync(client, booked, "Booked", DevelopmentFixtureManifest.Branches[0].Name);
            await AssertOwnershipAsync(client, booked.Id, data.StaffId, data.StaffEmail, true);
            await AssertOwnershipAsync(client, cancellable.Id, null, null, false);
            await AssertStatusAsync(client, Guid.NewGuid(), HttpStatusCode.NotFound);
            await AssertStatusAsync(client, other.Id, HttpStatusCode.NotFound);
            await AssertCancellationSnapshotAsync(data, cancellable);
            await AssertDetailAsync(client, cancellable, "Cancelled", DevelopmentFixtureManifest.Branches[0].Name);
            Assert.Equal(CheckInStatus.Succeeded,
                (await services.GetRequiredService<AppointmentCheckInService>()
                    .CheckInAsync(data.StaffId, booked.Id, CheckInTestData.Evidence, CancellationToken.None)).Status);
            await AssertDetailAsync(client, booked, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
            Assert.NotEqual(booked.Vehicle.MileageKilometres, CheckInTestData.Evidence.MileageKilometres);
            await AssertCustomerEligibilityDoesNotHideHistoryAsync(client, database, data, booked, cancellable);

            using var customer = factory.CreateClient();
            await LoginAsync(customer, data.CustomerEmail);
            await AssertStatusAsync(customer, booked.Id, HttpStatusCode.Forbidden);
        }

        using var restarted = fixture.CreateFactory();
        using var reader = restarted.CreateClient();
        reader.DefaultRequestHeaders.Authorization = authorization;
        await AssertDetailAsync(reader, booked, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
        await AssertDetailAsync(reader, cancellable, "Cancelled", DevelopmentFixtureManifest.Branches[0].Name);
        await AssertCurrentScopeAsync(reader, database, data, booked, other, otherBranch);
    }

    private async Task AssertCancellationSnapshotAsync(CheckInTestData data, AppointmentReceipt appointment)
    {
        var interceptor = new AfterActorReadInterceptor(async () =>
        {
            using var scope = data.Factory.Services.CreateScope();
            Assert.Equal(CancellationStatus.Succeeded,
                (await scope.ServiceProvider.GetRequiredService<AppointmentCancellationService>()
                    .CancelAsync(data.OwnerId, appointment.Id, CancellationToken.None)).Status);
        });
        var options = new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).AddInterceptors(interceptor).Options;
        await using var database = new DealershipDbContext(options);
        var snapshot = await new StaffAppointmentDetailStore(database)
            .GetAsync(data.StaffId, appointment.Id, CancellationToken.None);
        Assert.True(interceptor.Invoked);
        Assert.Equal(StaffAppointmentDetailStatus.Succeeded, snapshot.Status);
        Assert.NotNull(snapshot.Detail);
        Assert.Equal(appointment.Id, snapshot.Detail.Id);
        Assert.Equal("Booked", snapshot.Detail.Status);
        Assert.Equal(appointment.Vehicle.MileageKilometres, snapshot.Detail.MileageKilometres);
    }

    private static async Task AssertCustomerEligibilityDoesNotHideHistoryAsync(HttpClient client,
        DealershipDbContext database, CheckInTestData data, AppointmentReceipt checkedIn, AppointmentReceipt cancelled)
    {
        await database.Users.Where(user => user.Id == data.OwnerId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.IsActive, false).SetProperty(user => user.EmailConfirmed, false));

        try
        {
            await AssertDetailAsync(client, checkedIn, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
            await AssertDetailAsync(client, cancelled, "Cancelled", DevelopmentFixtureManifest.Branches[0].Name);
        }
        finally
        {
            await database.Users.Where(user => user.Id == data.OwnerId).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.IsActive, true).SetProperty(user => user.EmailConfirmed, true));
        }
    }

    private static async Task AssertCurrentScopeAsync(HttpClient client, DealershipDbContext database,
        CheckInTestData data, AppointmentReceipt booked, AppointmentReceipt other, Guid otherBranch)
    {
        foreach (var role in new[] { AccountRoles.WorkshopTechnician, AccountRoles.Administrator })
        {
            var branchId = role == AccountRoles.WorkshopTechnician ? (Guid?)data.BranchId : null;
            await SetStaffScopeAsync(database, data.StaffId, role, branchId);
            await AssertStatusAsync(client, booked.Id, HttpStatusCode.Forbidden);
            await AssertStatusAsync(client, Guid.NewGuid(), HttpStatusCode.Forbidden);
        }

        await SetStaffScopeAsync(database, data.StaffId, AccountRoles.GeneralManager, null);
        await AssertOwnershipAsync(client, booked.Id, data.StaffId, data.StaffEmail, false);
        await AssertDetailAsync(client, other, "Booked", "ApexAuto Petaling Jaya");
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        await AssertDetailAsync(client, booked, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
        await AssertStatusAsync(client, Guid.NewGuid(), HttpStatusCode.NotFound);
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, true));

        await SetStaffScopeAsync(database, data.StaffId, AccountRoles.BranchManager, otherBranch);
        await AssertDetailAsync(client, other, "Booked", "ApexAuto Petaling Jaya");
        await AssertStatusAsync(client, booked.Id, HttpStatusCode.NotFound);
        await SetStaffScopeAsync(database, data.StaffId, AccountRoles.SalesAgent, data.BranchId);
        await AssertOwnershipAsync(client, booked.Id, data.StaffId, data.StaffEmail, true);
        await AssertDetailAsync(client, booked, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        await AssertStatusAsync(client, booked.Id, HttpStatusCode.Unauthorized);
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, true));
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        await AssertStatusAsync(client, booked.Id, HttpStatusCode.Unauthorized);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await AssertDetailAsync(client, booked, "CheckedIn", DevelopmentFixtureManifest.Branches[0].Name);
    }

    private static Task<int> SetStaffScopeAsync(DealershipDbContext database, Guid staffId, string role, Guid? branchId)
    {
        return database.Users.Where(user => user.Id == staffId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Role, role).SetProperty(user => user.BranchId, branchId));
    }

    private static async Task AssertDetailAsync(HttpClient client, AppointmentReceipt expected, string status,
        string branchName)
    {
        using var response = await client.GetAsync($"{Route}/{expected.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DetailProperties, detail.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(expected.Id, detail.GetProperty("id").GetGuid());
        Assert.Equal(expected.BranchId, detail.GetProperty("branchId").GetGuid());
        Assert.Equal(branchName, detail.GetProperty("branchName").GetString());
        Assert.Equal("Nur Aina Abdullah", detail.GetProperty("customerName").GetString());
        Assert.Equal(expected.RegistrationPlate, detail.GetProperty("registrationPlate").GetString());
        Assert.Equal(expected.Slot.StartsAt, detail.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(expected.Slot.EndsAt, detail.GetProperty("endsAt").GetDateTimeOffset());
        Assert.Equal(status, detail.GetProperty("status").GetString());
        Assert.Equal(expected.Vehicle.Make, detail.GetProperty("make").GetString());
        Assert.Equal(expected.Vehicle.Model, detail.GetProperty("model").GetString());
        Assert.Equal(expected.Vehicle.Trim, detail.GetProperty("trim").GetString());
        Assert.Equal(expected.Vehicle.ManufactureYear, detail.GetProperty("manufactureYear").GetInt32());
        Assert.Equal(expected.Vehicle.MileageKilometres, detail.GetProperty("mileageKilometres").GetInt32());
    }

    private static async Task AssertStatusAsync(HttpClient client, Guid appointmentId, HttpStatusCode expected)
    {
        using var response = await client.GetAsync($"{Route}/{appointmentId}");
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static async Task AssertOwnershipAsync(HttpClient client, Guid appointmentId, Guid? handlerId,
        string? handlerEmail, bool eligible)
    {
        using var response = await client.GetAsync($"{Route}/{appointmentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<StaffAppointmentDetail>();
        if (handlerId is null)
        {
            Assert.Null(detail!.Ownership);
            return;
        }

        Assert.NotNull(detail!.Ownership);
        Assert.Equal(1, detail.Ownership.Version);
        Assert.Equal(handlerId, detail.Ownership.HandlerId);
        Assert.Equal(handlerEmail, detail.Ownership.HandlerEmail);
        Assert.Equal(eligible, detail.Ownership.HandlerEligible);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private sealed class AfterActorReadInterceptor(Func<Task> afterActorRead) : DbCommandInterceptor
    {
        public bool Invoked { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Invoked && command.CommandText.Contains("FROM \"AspNetUsers\"", StringComparison.Ordinal))
            {
                Invoked = true;
                await afterActorRead();
            }

            return result;
        }
    }
}
