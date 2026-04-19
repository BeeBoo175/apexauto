using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class ListingLifecycleTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ManagerDraftApprovalAndWithdrawalRetainExactScopedHistoryAcrossRacesAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var checkedIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var vehicleId = checkedIn.Receipt!.VehicleId;
        var (manager, managerEmail) = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var (group, _) = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var (technician, _) = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var otherBranch = Guid.NewGuid();
        await services.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[1] with { Id = otherBranch }, CancellationToken.None);
        var (outside, _) = await ProvisionAsync(services, otherBranch, AccountRoles.BranchManager);
        var request = new ListingDraftRequest(Guid.NewGuid(), 0, 60000m, 90m,
            VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected Malaysian hatchback");

        Assert.Equal(ListingStatus.Forbidden, (await SaveAsync(data.Factory, data.StaffId, vehicleId, request)).Status);
        Assert.Equal(ListingStatus.Forbidden, (await SaveAsync(data.Factory, technician, vehicleId, request)).Status);
        Assert.Equal(ListingStatus.NotFound, (await SaveAsync(data.Factory, outside, vehicleId, request)).Status);
        var replayRace = await Task.WhenAll(SaveAsync(data.Factory, manager, vehicleId, request),
            SaveAsync(data.Factory, manager, vehicleId, request));
        Assert.All(replayRace, result => Assert.Equal(ListingStatus.Succeeded, result.Status));
        var original = replayRace[0].Draft!;
        Assert.Equal(original.Id, replayRace[1].Draft!.Id);
        Assert.Equal(original.RecordedAt, replayRace[1].Draft!.RecordedAt);
        Assert.Equal(ListingStatus.Conflict,
            (await SaveAsync(data.Factory, manager, vehicleId, request with { VehiclePrice = 61000m })).Status);
        Assert.Equal(ListingStatus.Invalid,
            (await SaveAsync(data.Factory, manager, vehicleId, request with { OperationId = Guid.NewGuid(), VehiclePrice = 60000.001m })).Status);
        var publish = new ListingPublishRequest(Guid.NewGuid(), original.Id, Guid.NewGuid());
        Assert.Equal(ListingStatus.Conflict, (await PublishAsync(data.Factory, manager, vehicleId, publish)).Status);

        var assigned = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            vehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspectionId = assigned.Receipt!.InspectionId;
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, 0, []) { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] },
            CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var signed = await services.GetRequiredService<InspectionSignoffService>()
            .SignAsync(technician, inspectionId, new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        Assert.Equal(ListingStatus.Conflict, (await PublishAsync(data.Factory, manager, vehicleId, publish)).Status);
        publish = publish with { SignedRevisionId = signed.Report!.OperationId };
        var published = await PublishAsync(data.Factory, manager, vehicleId, publish);
        Assert.Equal(ListingStatus.Succeeded, published.Status);

        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var oldSnapshot = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"Status\" FROM \"Vehicles\" WHERE \"Id\" = @id", stale, oldSnapshot))
        {
            read.Parameters.AddWithValue("id", vehicleId);
            Assert.Equal("Available", await read.ExecuteScalarAsync());
        }

        var edits = await Task.WhenAll(
            SaveAsync(data.Factory, manager, vehicleId, request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, VehiclePrice = 59000m }),
            SaveAsync(data.Factory, group, vehicleId, request with { OperationId = Guid.NewGuid(), ExpectedVersion = 1, VehiclePrice = 58000m }));
        var latest = Assert.Single(edits, result => result.Status == ListingStatus.Succeeded).Draft!;
        Assert.Single(edits, result => result.Status == ListingStatus.Conflict);
        await using (var fence = new NpgsqlCommand("SELECT \"Id\" FROM \"Vehicles\" WHERE \"Id\" = @id FOR UPDATE", stale, oldSnapshot))
        {
            fence.Parameters.AddWithValue("id", vehicleId);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => fence.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.SerializationFailure, rejected.SqlState);
        }
        await oldSnapshot.RollbackAsync();
        var database = services.GetRequiredService<DealershipDbContext>();
        Assert.Single(await database.ListingPublications.Where(item => item.VehicleId == vehicleId).ToListAsync());
        Assert.Empty(await database.ListingWithdrawals.Where(item => item.VehicleId == vehicleId).ToListAsync());
        Assert.Equal(ListingStatus.Conflict,
            (await PublishAsync(data.Factory, manager, vehicleId, publish with { OperationId = Guid.NewGuid() })).Status);
        var nextPublish = new ListingPublishRequest(Guid.NewGuid(), latest.Id, signed.Report.OperationId);
        var approved = await PublishAsync(data.Factory, group, vehicleId, nextPublish);
        Assert.Equal(ListingStatus.Succeeded, approved.Status);
        Assert.Equal(ListingStatus.Conflict, (await PublishAsync(data.Factory, manager, vehicleId,
            nextPublish with { OperationId = original.Id })).Status);

        using var restarted = fixture.CreateFactory();
        var replay = await PublishAsync(restarted, group, vehicleId, nextPublish);
        Assert.Equal(approved.Publication!.Id, replay.Publication!.Id);
        Assert.Equal(approved.Publication.RecordedAt, replay.Publication.RecordedAt);
        var withdraw = new ListingWithdrawRequest(Guid.NewGuid(), approved.Publication.Id, "Manager withdraws pending listing correction");
        var withdrawn = await WithdrawAsync(restarted, manager, vehicleId, withdraw);
        Assert.Equal(ListingStatus.Succeeded, withdrawn.Status);
        Assert.Equal(withdrawn.Withdrawal!.Id, (await WithdrawAsync(restarted, manager, vehicleId, withdraw)).Withdrawal!.Id);
        Assert.Equal(ListingStatus.Conflict, (await WithdrawAsync(restarted, manager, vehicleId,
            withdraw with { OperationId = Guid.NewGuid() })).Status);

        await VerifyApiAsync(restarted, managerEmail, vehicleId, request);
        await VerifySqlAsync(original, data.StaffId);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(ListingStatus.Forbidden, (await SaveAsync(restarted, manager, vehicleId, request)).Status);
    }

    private async Task VerifySqlAsync(ListingRevision revision, Guid salesAgent)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var update = new NpgsqlCommand("UPDATE \"ListingRevisions\" SET \"VehiclePrice\" = 1 WHERE \"Id\" = @id", connection);
        update.Parameters.AddWithValue("id", revision.Id);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Equal("CK_Listings_Immutable", immutable.ConstraintName);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO "ListingRevisions" ("Id", "VehicleId", "BranchId", "ActorId", "Version",
                "VehiclePrice", "RoadTax", "Transmission", "Fuel", "Description", "RecordedAt")
            SELECT @operation, "VehicleId", "BranchId", @actor, 3,
                "VehiclePrice", "RoadTax", "Transmission", "Fuel", "Description", clock_timestamp()
            FROM "ListingRevisions" WHERE "Id" = @id
            """, connection);
        insert.Parameters.AddWithValue("operation", Guid.NewGuid());
        insert.Parameters.AddWithValue("actor", salesAgent);
        insert.Parameters.AddWithValue("id", revision.Id);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("CK_Listings_Eligibility", denied.ConstraintName);
    }

    private static async Task VerifyApiAsync(WebApplicationFactory<Program> factory, string email,
        Guid vehicleId, ListingDraftRequest original)
    {
        using var client = factory.CreateClient();
        var path = $"/api/staff/listings/{vehicleId}/drafts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, original)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        var session = (await login.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.PostAsJsonAsync(path, original);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var invalid = $$"""
            {"operationId":"{{Guid.NewGuid()}}","expectedVersion":2,"vehiclePrice":60000.0000000000000000001,
             "roadTax":90,"transmission":0,"fuel":0,"description":"Exact money is required"}
            """;
        using var content = new StringContent(invalid, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, content)).StatusCode);
    }

    private static async Task<(Guid Id, string Email)> ProvisionAsync(IServiceProvider services, Guid? branch, string role)
    {
        var id = Guid.NewGuid();
        var email = $"staff.{id:N}@example.invalid";
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, email, "Password123!", role, branch), CancellationToken.None);
        return (id, email);
    }

    private static async Task<ListingResult> SaveAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid vehicle, ListingDraftRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ListingService>().SaveAsync(actor, vehicle, request, CancellationToken.None);
    }

    private static async Task<ListingResult> PublishAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid vehicle, ListingPublishRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ListingService>().PublishAsync(actor, vehicle, request, CancellationToken.None);
    }

    private static async Task<ListingResult> WithdrawAsync(WebApplicationFactory<Program> factory,
        Guid actor, Guid vehicle, ListingWithdrawRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ListingService>().WithdrawAsync(actor, vehicle, request, CancellationToken.None);
    }
}
