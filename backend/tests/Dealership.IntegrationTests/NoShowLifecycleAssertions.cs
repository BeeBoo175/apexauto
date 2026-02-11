using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

internal static class NoShowLifecycleAssertions
{
    public static async Task VerifyAsync(IntakePhotoFixture fixture)
    {
        using var factory = fixture.CreateFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var row = DevelopmentIntakeManifest.Appointments.First(item => item.Status == StaffWorklistStatus.NoShow);
        var appointment = await database.IntakeAppointments.SingleAsync(item => item.Id == row.Id);
        var manager = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == row.BranchId && item.Role == AccountRoles.BranchManager);
        var agent = DevelopmentFixtureManifest.Staff.Single(item =>
            item.BranchId == row.BranchId && item.Role == AccountRoles.SalesAgent);
        var otherManager = DevelopmentFixtureManifest.Staff.First(item =>
            item.BranchId != row.BranchId && item.Role == AccountRoles.BranchManager);
        var evidence = new CheckInEvidence("PL1BT65R84B123459", 92500, true, true, true, true, "Attendance verified");
        var correction = new NoShowCorrectionRequest(appointment.StartsAt.AddTicks(1), "Arrival was recorded in the branch log.");
        var originalCount = await database.AppointmentCheckIns.CountAsync();
        var detail = await services.GetRequiredService<IStaffAppointmentDetailStore>().GetAsync(manager.Id, row.Id, CancellationToken.None);
        Assert.Equal("NoShow", detail.Detail!.Status);
        Assert.Equal(AppointmentDay.EndsAt(appointment.StartsAt), detail.Detail.NoShowAt);
        var query = new StaffWorklistQuery(null, null, StaffWorklistStatus.NoShow, null, 1, 20);
        var worklist = services.GetRequiredService<IStaffWorklistStore>();
        Assert.Equal(row.Id, Assert.Single((await worklist.ListAsync(manager.Id, query, CancellationToken.None))!.Items).Id);
        Assert.DoesNotContain((await worklist.ListAsync(manager.Id, query with { Status = StaffWorklistStatus.Booked }, CancellationToken.None))!.Items,
            item => item.Id == row.Id);
        var history = await services.GetRequiredService<IAppointmentHistoryStore>().ListAsync(appointment.OwnerId,
            new AppointmentHistoryQuery(null, null, 3, 20), CancellationToken.None);
        Assert.Contains(history!.Items, item => item.Id == row.Id && item.NoShowAt == detail.Detail.NoShowAt);
        Assert.Equal(originalCount, await database.AppointmentCheckIns.CountAsync());
        Assert.Equal(CheckInStatus.Forbidden, (await CorrectAsync(factory, agent.Id, row.Id, evidence, correction)).Status);
        Assert.Equal(CheckInStatus.NotFound, (await CorrectAsync(factory, otherManager.Id, row.Id, evidence, correction)).Status);
        Assert.Equal(CheckInStatus.Unavailable, (await CorrectAsync(factory, manager.Id, row.Id, evidence,
            correction with { AttendedAt = AppointmentDay.EndsAt(appointment.StartsAt) })).Status);
        Assert.Equal(CheckInStatus.Invalid, (await CorrectAsync(factory, manager.Id, row.Id, evidence,
            correction with { Reason = " " })).Status);
        await VerifyRestrictedSqlAsync(fixture, appointment, agent.Id, manager.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            CorrectAsync(factory, manager.Id, row.Id, evidence, correction)));
        Assert.All(results, result => Assert.Equal(CheckInStatus.Succeeded, result.Status));
        Assert.Equal(results[0].Receipt, results[1].Receipt);
        Assert.NotNull(results[0].Receipt!.Correction);
        Assert.Equal(appointment.StartsAt, results[0].Receipt!.CheckedInAt);
        using var restarted = fixture.CreateFactory();
        Assert.Equal(results[0].Receipt, (await CorrectAsync(restarted, manager.Id, row.Id, evidence, correction)).Receipt);
        Assert.Equal(CheckInStatus.Conflict, (await CorrectAsync(restarted, manager.Id, row.Id, evidence,
            correction with { Reason = "Different explanation" })).Status);
        var corrected = await services.GetRequiredService<IStaffAppointmentDetailStore>().GetAsync(manager.Id, row.Id, CancellationToken.None);
        Assert.Equal("CheckedIn", corrected.Detail!.Status);
        Assert.Equal(detail.Detail.NoShowAt, corrected.Detail.NoShowAt);
        Assert.Equal(manager.Id, corrected.Detail.CorrectedBy);
        Assert.Equal(correction.Reason, corrected.Detail.CorrectionReason);
        Assert.Empty((await worklist.ListAsync(manager.Id, query, CancellationToken.None))!.Items);
        Assert.Equal(originalCount + 1, await database.AppointmentCheckIns.CountAsync());
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var change = new NpgsqlCommand("UPDATE \"AppointmentCheckIns\" SET \"CorrectionReason\" = 'Rewritten' WHERE \"AppointmentId\" = @id", connection);
        change.Parameters.AddWithValue("id", row.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => change.ExecuteNonQueryAsync());
        Assert.Equal("CK_IntakeAppointments_Immutable", error.ConstraintName);
    }

    private static async Task VerifyRestrictedSqlAsync(IntakePhotoFixture fixture, IntakeAppointment appointment,
        Guid agentId, Guid managerId)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        foreach (var actor in new[] { agentId, managerId })
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO "AppointmentCheckIns" ("AppointmentId", "OwnerId", "BranchId", "VehicleId", "StaffId", "CheckedInAt",
                    "Vin", "MileageKilometres", "CustomerPresent", "IdentityConfirmed", "OwnershipConfirmed", "VehicleConfirmed",
                    "CorrectionRecordedAt", "CorrectionReason")
                VALUES (@id, @owner, @branch, @vehicle, @actor, @at,
                    'PL1BT65R84B123459', 92500, true, true, true, true, @recorded, @reason)
                """, connection);
            command.Parameters.AddWithValue("id", appointment.Id);
            command.Parameters.AddWithValue("owner", appointment.OwnerId);
            command.Parameters.AddWithValue("branch", appointment.BranchId);
            command.Parameters.AddWithValue("vehicle", Guid.NewGuid());
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("at", appointment.StartsAt);
            command.Parameters.AddWithValue("recorded", actor == agentId ? DateTimeOffset.UtcNow : appointment.StartsAt);
            command.Parameters.AddWithValue("reason", "Branch log confirmation");
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_AppointmentCheckIns_Time", error.ConstraintName);
        }
    }

    private static async Task<CheckInResult> CorrectAsync(WebApplicationFactory<Program> factory, Guid staffId,
        Guid appointmentId, CheckInEvidence evidence, NoShowCorrectionRequest correction)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAppointmentCheckInStore>()
            .CorrectNoShowAsync(staffId, appointmentId, evidence, correction, CancellationToken.None);
    }
}
