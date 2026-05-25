using System.Data;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class InspectionLifecycleTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task ReadinessRequiresCompleteCurrentRepairsAndReopeningRemovesAvailabilityWithoutRegrading()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        await data.ClaimAsync(appointment.Id);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var checkedIn = await services.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(data.StaffId, appointment.Id, CheckInTestData.Evidence, CancellationToken.None);
        var vehicle = checkedIn.Receipt!.VehicleId;
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var assignment = await services.GetRequiredService<InspectionAssignmentService>()
            .AssignAsync(technician, vehicle, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspection = assignment.Receipt!.InspectionId;
        var reports = services.GetRequiredService<IInspectionReportStore>();
        var reopenings = services.GetRequiredService<InspectionReopeningService>();
        var signoffs = services.GetRequiredService<InspectionSignoffService>();
        var saves = services.GetRequiredService<InspectionDraftSaveService>();
        var requests = services.GetRequiredService<InspectionRepairRequestService>();
        var completions = services.GetRequiredService<InspectionRepairCompletionService>();
        await AssertStatusAsync(database, vehicle, VehicleStatus.PendingInspection);
        await AssertForgedStatusAsync(vehicle, VehicleStatus.Available);
        await AssertForgedStatusAsync(vehicle, VehicleStatus.Rejected);

        // One vehicle and host exercise the distinct classification boundaries across retained revisions.
        var cases = new (InspectionSeverity? Severity, decimal Estimate, VehicleStatus Status)[]
        {
            (null, 0, VehicleStatus.Available),
            (InspectionSeverity.Advisory, 0, VehicleStatus.Available),
            (InspectionSeverity.MinorDefect, 0, VehicleStatus.PendingInspection),
            (InspectionSeverity.MajorDefect, 0, VehicleStatus.PendingInspection),
            (InspectionSeverity.Advisory, 1, VehicleStatus.PendingInspection),
            (InspectionSeverity.CriticalDisqualifier, 0, VehicleStatus.Rejected),
            (InspectionSeverity.Advisory, 15000.01m, VehicleStatus.Rejected)
        };
        long revision = 0;
        long repairVersion = 0;
        foreach (var (severity, estimate, status) in cases)
        {
            revision++;
            IReadOnlyList<InspectionDraftEntryInput> entries = severity is null ? [] :
                [new("EP-004", InspectionEntryKind.Finding, severity, estimate, "Front bumper finding")];
            var saved = await saves.SaveAsync(technician, inspection, new(Guid.NewGuid(), 1, revision - 1, entries)
            {
                ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
            }, CancellationToken.None);
            Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
            var sign = new InspectionSignRequest(Guid.NewGuid(), 1, revision, revision);
            var signed = (await signoffs.SignAsync(technician, inspection, sign, CancellationToken.None)).Report!;
            Assert.NotNull(signed);
            await AssertStatusAsync(database, vehicle, status);
            Assert.Equal(JsonSerializer.Serialize(signed), JsonSerializer.Serialize((await signoffs
                .SignAsync(technician, inspection, sign, CancellationToken.None)).Report));

            if (status == VehicleStatus.PendingInspection ||
                severity == InspectionSeverity.Advisory && estimate == 0)
            {
                var request = (await requests.RequestAsync(technician, inspection,
                    new(Guid.NewGuid(), 1, revision, repairVersion, [new("EP-004", "Rectify and reinspect bumper")], 0),
                    CancellationToken.None)).Receipt!;
                repairVersion++;
                Assert.NotNull(request);
                await AssertStatusAsync(database, vehicle, VehicleStatus.PendingInspection);
                await AssertForgedStatusAsync(vehicle, VehicleStatus.Available);
                var completion = new InspectionRepairCompletionRequest(Guid.NewGuid(), request.OperationId, 1,
                    revision, repairVersion, "Finding rectified and reinspected.");
                Assert.Equal(InspectionAssignmentStatus.Succeeded,
                    (await completions.CompleteAsync(technician, inspection, completion, CancellationToken.None)).Status);
                await AssertStatusAsync(database, vehicle, VehicleStatus.Available);
                Assert.Equal(JsonSerializer.Serialize(signed), JsonSerializer.Serialize((await reports
                    .ReadAsync(manager, inspection, revision, CancellationToken.None)).Report));
            }

            if (status == VehicleStatus.Rejected)
            {
                await AssertForgedStatusAsync(vehicle, VehicleStatus.Available);
                Assert.Equal(InspectionAssignmentStatus.Conflict, (await requests.RequestAsync(technician, inspection,
                    new(Guid.NewGuid(), 1, revision, repairVersion, [new("EP-004", "Cannot repair rejected report")], 0),
                    CancellationToken.None)).Status);
            }

            var reopen = new InspectionReopenRequest(Guid.NewGuid(), 1, revision, "Manager requests a fresh examination");
            Assert.Equal(InspectionAssignmentStatus.Succeeded,
                (await reopenings.ReopenAsync(manager, inspection, reopen, CancellationToken.None)).Status);
            await AssertStatusAsync(database, vehicle, VehicleStatus.PendingInspection);
            Assert.Equal(JsonSerializer.Serialize(signed), JsonSerializer.Serialize((await reports
                .ReadAsync(manager, inspection, revision, CancellationToken.None)).Report));
        }

        revision++;
        var partialEntries = new InspectionDraftEntryInput[]
        {
            new("EP-004", InspectionEntryKind.Finding, InspectionSeverity.MinorDefect, 0, "Bumper alignment"),
            new("EP-005", InspectionEntryKind.Finding, InspectionSeverity.MajorDefect, 0, "Rear bumper attachment")
        };
        await saves.SaveAsync(technician, inspection, new(Guid.NewGuid(), 1, revision - 1, partialEntries)
        {
            ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)]
        }, CancellationToken.None);
        await signoffs.SignAsync(technician, inspection, new(Guid.NewGuid(), 1, revision, revision), CancellationToken.None);
        var partial = (await requests.RequestAsync(technician, inspection, new(Guid.NewGuid(), 1, revision,
            repairVersion, [new("EP-004", "Align bumper")], 0), CancellationToken.None)).Receipt!;
        repairVersion++;
        await completions.CompleteAsync(technician, inspection, new(Guid.NewGuid(), partial.OperationId, 1, revision,
            repairVersion, "Front bumper reinspected."), CancellationToken.None);
        await AssertStatusAsync(database, vehicle, VehicleStatus.PendingInspection);
        await AssertForgedStatusAsync(vehicle, VehicleStatus.Available);
        var complete = (await requests.RequestAsync(technician, inspection, new(Guid.NewGuid(), 1, revision,
            repairVersion, [new("EP-004", "Align bumper"), new("EP-005", "Secure rear bumper")], 0),
            CancellationToken.None)).Receipt!;
        repairVersion++;
        var completedRequest = new InspectionRepairCompletionRequest(Guid.NewGuid(), complete.OperationId, 1, revision,
            repairVersion, "Both findings rectified and reinspected.");
        await completions.CompleteAsync(technician, inspection, completedRequest, CancellationToken.None);
        await AssertStatusAsync(database, vehicle, VehicleStatus.Available);
        await AssertForgedStatusAsync(vehicle, VehicleStatus.PendingInspection);
        await InspectionLifecycleMigrationAssertions.VerifyAsync(fixture.Database, services);

        using var restarted = fixture.CreateFactory();
        using var restored = restarted.Services.CreateScope();
        Assert.Equal(InspectionAssignmentStatus.Succeeded, (await restored.ServiceProvider
            .GetRequiredService<InspectionRepairCompletionService>()
            .CompleteAsync(technician, inspection, completedRequest, CancellationToken.None)).Status);
        await AssertStatusAsync(database, vehicle, VehicleStatus.Available);

        await using var stale = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await stale.OpenAsync();
        await using var transaction = await stale.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using (var read = new NpgsqlCommand("SELECT \"Status\" FROM \"Vehicles\" WHERE \"Id\" = @id", stale, transaction))
        {
            read.Parameters.AddWithValue("id", vehicle);
            Assert.Equal("Available", await read.ExecuteScalarAsync());
        }
        var renewed = await requests.RequestAsync(technician, inspection, new(Guid.NewGuid(), 1, revision,
            repairVersion, complete.Items, 1), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, renewed.Status);
        await AssertStatusAsync(database, vehicle, VehicleStatus.PendingInspection);
        await using var write = new NpgsqlCommand("UPDATE \"Vehicles\" SET \"Status\" = 'Available' WHERE \"Id\" = @id", stale, transaction);
        write.Parameters.AddWithValue("id", vehicle);
        var error = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, error.SqlState);
    }

    private async Task AssertForgedStatusAsync(Guid vehicle, VehicleStatus status)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.RuntimeConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE "Vehicles" SET "Status" = @status WHERE "Id" = @id;
            SET CONSTRAINTS ALL IMMEDIATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("id", vehicle);
        command.Parameters.AddWithValue("status", status.ToString());
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("CK_Vehicles_InspectionLifecycle", error.ConstraintName);
    }

    private static async Task AssertStatusAsync(DealershipDbContext database, Guid vehicle, VehicleStatus status)
    {
        Assert.Equal(status, (await database.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicle)).Status);
    }

    private static async Task<Guid> ProvisionAsync(IServiceProvider services, Guid branch, string role)
    {
        var id = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, $"amir.hakim.{id:N}@example.invalid", "Password123!", role, branch), CancellationToken.None);
        return id;
    }
}
