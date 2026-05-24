using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            appointment.Id, AppointmentAssignmentKind.Claimed, claim.Request)));
        var winner = Array.FindIndex(results, result => result.Status == AppointmentAssignmentStatus.Succeeded);
        Assert.Single(results, result => result.Status == AppointmentAssignmentStatus.Succeeded);
        var lost = Assert.Single(results, result => result.Status == AppointmentAssignmentStatus.Conflict);
        Assert.Equal(claims[winner].Actor, lost.Current!.HandlerId);
        var claimed = results[winner].Receipt!;
        var replacement = claims[1 - winner].Actor;
        var request = new AppointmentAssignmentRequest(Guid.NewGuid(), 1, replacement, "  Cover customer arrival  ");
        Assert.Equal(AppointmentAssignmentStatus.Forbidden,
            (await ExecuteAsync(data.Factory, replacement, appointment.Id, AppointmentAssignmentKind.Assigned, request)).Status);
        var assigned = await ExecuteAsync(data.Factory, manager, appointment.Id, AppointmentAssignmentKind.Assigned, request);
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, assigned.Status);
        Assert.Equal(claimed.HandlerId, assigned.Receipt!.PreviousHandlerId);
        Assert.Equal("Cover customer arrival", assigned.Receipt.Reason);
        using var restarted = fixture.CreateFactory();
        var replay = await ExecuteAsync(restarted, claims[winner].Actor, appointment.Id,
            AppointmentAssignmentKind.Claimed, claims[winner].Request);
        Assert.Equal(claimed.Id, replay.Receipt!.Id);
        Assert.Equal(claimed.RecordedAt, replay.Receipt.RecordedAt);
        Assert.Equal(replacement, replay.Current!.HandlerId);
        Assert.Equal(AppointmentAssignmentStatus.Conflict, (await ExecuteAsync(restarted, manager, appointment.Id,
            AppointmentAssignmentKind.Assigned, request with { Reason = "Different handover" })).Status);
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(restarted, claims[winner].Actor,
            appointment.Id, AppointmentAssignmentKind.Released, new(Guid.NewGuid(), 2))).Status);
        var released = await ExecuteAsync(restarted, replacement, appointment.Id,
            AppointmentAssignmentKind.Released, new(Guid.NewGuid(), 2));
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, released.Status);
        Assert.Null(released.Receipt!.HandlerId);
        var reclaimed = await ExecuteAsync(restarted, manager, appointment.Id,
            AppointmentAssignmentKind.Claimed, new(Guid.NewGuid(), 3));
        Assert.Equal(AppointmentAssignmentStatus.Succeeded, reclaimed.Status);

        var database = services.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(manager, (await database.AppointmentAssignments.OrderByDescending(item => item.Version).FirstAsync()).HandlerId);
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(restarted, manager,
            appointment.Id, AppointmentAssignmentKind.Claimed, new(claimed.Id, 0))).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await AssertSqlGuardsAsync(claimed, manager, replacement);
        await AssertTerminalAndIneligibleAsync(data, manager);
    }

    private static async Task AssertTerminalAndIneligibleAsync(CheckInTestData data, Guid manager)
    {
        var appointment = await data.BookAsync(slot: 1, plate: "BPL 6392", dateOffset: 1);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var group = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var database = services.GetRequiredService<DealershipDbContext>();
        Assert.Equal(AppointmentAssignmentStatus.Forbidden, (await ExecuteAsync(data.Factory, group, appointment.Id,
            AppointmentAssignmentKind.Claimed, new(Guid.NewGuid(), 0))).Status);
        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(AppointmentAssignmentStatus.Unavailable, (await ExecuteAsync(data.Factory, manager, appointment.Id,
            AppointmentAssignmentKind.Assigned, new(Guid.NewGuid(), 0, data.StaffId))).Status);
        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        Assert.Equal(CancellationStatus.Succeeded, (await services.GetRequiredService<AppointmentCancellationService>()
            .CancelAsync(data.OwnerId, appointment.Id, CancellationToken.None)).Status);
        Assert.Equal(AppointmentAssignmentStatus.Unavailable, (await ExecuteAsync(data.Factory, data.StaffId, appointment.Id,
            AppointmentAssignmentKind.Claimed, new(Guid.NewGuid(), 0))).Status);
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
            $"farid.hakim.{id:N}@example.invalid", "Password123!", role, branchId), CancellationToken.None);
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
