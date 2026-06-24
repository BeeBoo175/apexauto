using Dealership.Domain.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class AppointmentAssignmentTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ClaimsSerializeAndRetainExactHistoryAcrossReassignmentReleaseAndRestart()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync(dateOffset: 1);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var second = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var claims = new[] { data.StaffId, second }.Select(actor =>
            (Actor: actor, Request: new AppointmentAssignmentRequest(Guid.NewGuid(), 0))).ToArray();
        var results = await Task.WhenAll(claims.Select(claim => ExecuteAsync(data.Factory, claim.Actor,
            appointment.Id, WorkOwnershipKind.Claimed, claim.Request)));
        var winner = Array.FindIndex(results, result => result.Status == AppointmentAssignmentStatus.Succeeded);
        Assert.Single(results, result => result.Status == AppointmentAssignmentStatus.Succeeded);
        var lost = Assert.Single(results, result => result.Status == AppointmentAssignmentStatus.Conflict);
        Assert.Equal(claims[winner].Actor, lost.Current!.HandlerId);
        var claimed = results[winner].Receipt!;
        var replacement = claims[1 - winner].Actor;
        var request = new AppointmentAssignmentRequest(Guid.NewGuid(), 1, replacement, "  Cover customer arrival  ");
        Assert.Equal(AppointmentAssignmentStatus.Forbidden,
            (await ExecuteAsync(data.Factory, replacement, appointment.Id, WorkOwnershipKind.Assigned, request)).Status);
        var assigned = await ExecuteAsync(data.Factory, manager, appointment.Id, WorkOwnershipKind.Assigned, request);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, assigned.Status);
        Assert.Equal(claimed.HandlerId, assigned.Receipt!.PreviousHandlerId);
        Assert.Equal("Cover customer arrival", assigned.Receipt.Reason);
        using var restarted = fixture.CreateFactory();
        var replay = await ExecuteAsync(restarted, claims[winner].Actor, appointment.Id,
            WorkOwnershipKind.Claimed, claims[winner].Request);
        Assert.Equal(claimed.Id, replay.Receipt!.Id);
        Assert.Equal(claimed.RecordedAt, replay.Receipt.RecordedAt);
        Assert.Equal(replacement, replay.Current!.HandlerId);
        Assert.Equal(AppointmentAssignmentStatus.Conflict, (await ExecuteAsync(restarted, manager, appointment.Id,
            WorkOwnershipKind.Assigned, request with { Reason = "Different handover" })).Status);
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(restarted, claims[winner].Actor,
            appointment.Id, WorkOwnershipKind.Released, new(Guid.NewGuid(), 2))).Status);
        var released = await ExecuteAsync(restarted, replacement, appointment.Id,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 2));
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, released.Status);
        Assert.Null(released.Receipt!.HandlerId);
        var reclaimed = await ExecuteAsync(restarted, manager, appointment.Id,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 3));
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, reclaimed.Status);

        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(manager, (await database.AppointmentAssignments.OrderByDescending(item => item.Version).FirstAsync()).HandlerId);
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(restarted, manager,
            appointment.Id, WorkOwnershipKind.Claimed, new(claimed.Id, 0))).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await AssertSqlGuardsAsync(claimed, manager, replacement);
        await AssertTerminalAndIneligibleAsync(data, manager);
        await AssertHttpCommandsAsync(data, manager);
        await AssertCheckInOwnershipAsync(data, manager);
    }

    private static async Task AssertCheckInOwnershipAsync(CheckInTestData data, Guid manager)
    {
        var appointment = await data.BookAsync(plate: "BPS 4187");
        Assert.Equal(CheckInStatus.Forbidden, (await CheckInAsync(data, data.StaffId, appointment.Id)).Status);
        Assert.Equal(CheckInStatus.Forbidden, (await CheckInAsync(data, manager, appointment.Id)).Status);
        await data.ClaimAsync(appointment.Id);
        Assert.Equal(CheckInStatus.Forbidden, (await CheckInAsync(data, manager, appointment.Id)).Status);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 1, manager, "Handle customer arrival"))).Status);
        Assert.Equal(CheckInStatus.Forbidden, (await CheckInAsync(data, data.StaffId, appointment.Id)).Status);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 2))).Status);
        Assert.Equal(CheckInStatus.Forbidden, (await CheckInAsync(data, manager, appointment.Id)).Status);

        using var scope = data.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.False(await database.Vehicles.AnyAsync(item => item.IntakeAppointmentId == appointment.Id));
        Assert.False(await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == appointment.Id));
        Assert.False(await database.AppointmentMailOutbox.AnyAsync(item =>
            item.AppointmentId == appointment.Id && item.Kind == Dealership.Infrastructure.Intake.AppointmentMailKind.CheckIn));
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 3))).Status);
        var checkedIn = await CheckInAsync(data, manager, appointment.Id);
        Assert.Equal(CheckInStatus.Succeeded, checkedIn.Status);
        Assert.Equal(manager, checkedIn.Receipt!.StaffId);
        Assert.Equal(checkedIn.Receipt, (await CheckInAsync(data, data.StaffId, appointment.Id)).Receipt);
        Assert.Equal(AppointmentAssignmentStatus.Unavailable, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            WorkOwnershipKind.Released, new(Guid.NewGuid(), 4))).Status);
        await AssertCheckInHandoffRaceAsync(data, manager);
    }

    private static async Task AssertCheckInHandoffRaceAsync(CheckInTestData data, Guid manager)
    {
        var appointment = await data.BookAsync(1, "BPS 4188");
        await data.ClaimAsync(appointment.Id);
        var evidence = CheckInTestData.Evidence with { Vin = "PL1BT65R84B123457" };
        var arrival = CheckInAsync(data, data.StaffId, appointment.Id, evidence);
        var handoff = ExecuteAsync(data.Factory, manager, appointment.Id, WorkOwnershipKind.Assigned,
            new(Guid.NewGuid(), 1, manager, "Cover customer arrival"));
        await Task.WhenAll(arrival, handoff);
        if (arrival.Result.Status == CheckInStatus.Succeeded)
        {
            Assert.Equal(AppointmentAssignmentStatus.Unavailable, handoff.Result.Status);
            Assert.Equal(data.StaffId, arrival.Result.Receipt!.StaffId);
            return;
        }

        Assert.Equal(CheckInStatus.Forbidden, arrival.Result.Status);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, handoff.Result.Status);
        var confirmed = await CheckInAsync(data, manager, appointment.Id, evidence);
        Assert.Equal(CheckInStatus.Succeeded, confirmed.Status);
        Assert.Equal(manager, confirmed.Receipt!.StaffId);
    }

    private static async Task<CheckInResult> CheckInAsync(CheckInTestData data, Guid actorId, Guid appointmentId,
        CheckInEvidence? evidence = null)
    {
        using var scope = data.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(actorId, appointmentId, evidence ?? CheckInTestData.Evidence, CancellationToken.None);
    }

    private async Task AssertHttpCommandsAsync(CheckInTestData data, Guid manager)
    {
        var appointment = await data.BookAsync(slot: 2, plate: "BPS 4186", dateOffset: 1);
        var path = $"/api/staff/intake/appointments/{appointment.Id}";
        var request = new AppointmentAssignmentRequest(Guid.NewGuid(), 0);
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{path}/claims", request)).StatusCode);
        await LoginAsync(client, data.StaffEmail);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"{path}/claims", new { request.OperationId })).StatusCode);
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{path}/claims", malformed)).StatusCode);
        using var oversized = new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync($"{path}/claims", oversized)).StatusCode);
        using var wrongType = new StringContent("{}");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync($"{path}/claims", wrongType)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/staff/intake/appointments/{Guid.NewGuid()}/claims", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{path}/assignments",
            request with { HandlerId = manager })).StatusCode);
        var claim = await client.PostAsJsonAsync($"{path}/claims", request);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        Assert.True(claim.Headers.CacheControl?.NoStore);
        var claimed = (await claim.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(data.StaffId, claimed["receipt"]!["handlerId"]!.GetValue<Guid>());
        var stale = await client.PostAsJsonAsync($"{path}/claims", request with { OperationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(stale.Headers.CacheControl?.NoStore);
        var conflict = (await stale.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("intake-ownership-conflict", conflict["code"]!.GetValue<string>());
        Assert.Equal(data.StaffId, conflict["current"]!["handlerId"]!.GetValue<Guid>());

        using var scope = data.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var managerEmail = await database.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync();
        using var managerClient = factory.CreateClient();
        await LoginAsync(managerClient, managerEmail!);
        var assignment = new AppointmentAssignmentRequest(Guid.NewGuid(), 1, manager, "Cover arrival");
        Assert.Equal(HttpStatusCode.OK, (await managerClient.PostAsJsonAsync($"{path}/assignments", assignment)).StatusCode);
        var replay = await client.PostAsJsonAsync($"{path}/claims", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var reconciled = (await replay.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(claimed["receipt"]!.ToJsonString(), reconciled["receipt"]!.ToJsonString());
        Assert.Equal(manager, reconciled["current"]!["handlerId"]!.GetValue<Guid>());
        using var lostReceipt = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppointmentAssignmentStore>();
            services.AddScoped<IAppointmentAssignmentStore>(provider => new LostReceiptStore(
                new Dealership.Infrastructure.Intake.AppointmentAssignmentStore(provider.GetRequiredService<DealershipDbContext>())));
        }));
        using var uncertainClient = lostReceipt.CreateClient();
        uncertainClient.DefaultRequestHeaders.Authorization = managerClient.DefaultRequestHeaders.Authorization;
        var release = new AppointmentAssignmentRequest(Guid.NewGuid(), 2);
        var uncertain = await uncertainClient.PostAsJsonAsync($"{path}/releases", release);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, uncertain.StatusCode);
        Assert.True(uncertain.Headers.CacheControl?.NoStore);
        Assert.Contains("unconfirmed", await uncertain.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await managerClient.PostAsJsonAsync($"{path}/releases", release)).StatusCode);
        using var customer = factory.CreateClient();
        await LoginAsync(customer, data.CustomerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"{path}/claims", request)).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    private sealed class LostReceiptStore(IAppointmentAssignmentStore inner) : IAppointmentAssignmentStore
    {
        public async Task<IAppointmentAssignmentTransaction> LockAsync(Guid actorId, Guid appointmentId,
            AppointmentAssignmentRequest request, CancellationToken cancellationToken)
        {
            return new LostReceiptTransaction(await inner.LockAsync(actorId, appointmentId, request, cancellationToken));
        }
    }

    private sealed class LostReceiptTransaction(IAppointmentAssignmentTransaction inner) : IAppointmentAssignmentTransaction
    {
        public AppointmentAssignmentContext Context => inner.Context;

        public Task<DateTimeOffset> ReadDatabaseTimeAsync(CancellationToken cancellationToken)
        {
            return inner.ReadDatabaseTimeAsync(cancellationToken);
        }

        public Task<bool> HasOverlapAsync(Guid handlerId, CancellationToken cancellationToken)
        {
            return inner.HasOverlapAsync(handlerId, cancellationToken);
        }

        public async Task CommitAsync(AppointmentAssignment assignment, CancellationToken cancellationToken)
        {
            Assert.True(cancellationToken.CanBeCanceled);
            await inner.CommitAsync(assignment, cancellationToken);
            throw new OperationCanceledException("The committed response was lost.");
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private static async Task AssertTerminalAndIneligibleAsync(CheckInTestData data, Guid manager)
    {
        var appointment = await data.BookAsync(slot: 1, plate: "BPL 6392", dateOffset: 1);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var database = services.GetRequiredService<DealershipDbContext>();
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(data.Factory, group, appointment.Id,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 0))).Status);
        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(AppointmentAssignmentStatus.Unavailable, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 0, data.StaffId))).Status);
        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        Assert.Equal(CancellationStatus.Succeeded, (await services.GetRequiredService<AppointmentCancellationService>()
            .CancelAsync(data.OwnerId, appointment.Id, CancellationToken.None)).Status);
        Assert.Equal(AppointmentAssignmentStatus.Unavailable, (await ExecuteAsync(data.Factory, data.StaffId, appointment.Id,
            WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 0))).Status);
        Assert.False(await database.AppointmentAssignments.AnyAsync(item => item.AppointmentId == appointment.Id));
    }

    private async Task AssertSqlGuardsAsync(AppointmentAssignment claim, Guid manager, Guid handler)
    {
        var insert = """
            INSERT INTO "AppointmentAssignments" ("Id", "AppointmentId", "BranchId", "ActorId", "Kind",
                "HandlerId", "PreviousHandlerId", "ExpectedVersion", "Version", "Reason", "RecordedAt")
            VALUES (@id, @appointment, @branch, @manager, 'Assigned', @handler, @manager, 4, 5,
                'Cover customer arrival', clock_timestamp());
            """;
        foreach (var (sql, constraint) in new[]
        {
            ("UPDATE \"AppointmentAssignments\" SET \"Reason\" = 'Changed' WHERE \"Id\" = @claim", "CK_AppointmentAssignments_Immutable"),
            (insert.Replace("4, 5", "3, 4", StringComparison.Ordinal), "CK_AppointmentAssignments_Chain"),
            (insert.Replace("'Cover customer arrival'", "NULL", StringComparison.Ordinal), "CK_AppointmentAssignments_Transition"),
            (insert.Replace("@manager, 'Assigned'", "@handler, 'Assigned'", StringComparison.Ordinal), "CK_AppointmentAssignments_Eligibility")
        })
        {
            await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("appointment", claim.AppointmentId);
            command.Parameters.AddWithValue("branch", claim.BranchId);
            command.Parameters.AddWithValue("manager", manager);
            command.Parameters.AddWithValue("handler", handler);
            command.Parameters.AddWithValue("claim", claim.Id);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(constraint, error.ConstraintName);
        }
    }

    private static async Task<Guid> ProvisionAsync(IServiceProvider services, Guid? branchId, string role)
    {
        var id = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(new(id,
            $"farid.hakim.{id:N}@example.invalid", "Password123!", role, branchId, "Amir Hakim", "0123456789"), CancellationToken.None);
        return id;
    }

    private static async Task<AppointmentAssignmentResult> ExecuteAsync(WebApplicationFactory<Program> factory,
        Guid actorId, Guid appointmentId, string kind, AppointmentAssignmentRequest request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppointmentAssignmentService>()
            .ExecuteAsync(actorId, appointmentId, kind, request, CancellationToken.None);
    }
}
