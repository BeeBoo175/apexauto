using Dealership.Domain.Common;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.TestDrives;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

internal static class TestDriveHandlingAssertions
{
    internal static async Task VerifyAsync(CheckInTestData data, IntakePhotoFixture fixture,
        ListingRecord listing, Guid manager, Guid technician)
    {
        using var setup = data.Factory.Services.CreateScope();
        var services = setup.ServiceProvider;
        await using var database = NewContext();
        var customer = await RegisterCustomerAsync(services, database);
        var otherCustomer = await RegisterCustomerAsync(services, database);
        var agent = await ProvisionAsync(services, data.BranchId, AccountRoles.SalesAgent);
        var pendingRepair = await database.InspectionRepairRequests.AsNoTracking()
            .Where(item => item.InspectionId == listing.InspectionId &&
                !database.InspectionRepairCompletions.Any(completion => completion.RequestId == item.Id))
            .OrderByDescending(item => item.Version).FirstAsync();
        Assert.Equal(InspectionAssignmentStatus.Succeeded,
            (await services.GetRequiredService<InspectionRepairCompletionService>().CompleteAsync(technician,
                listing.InspectionId, new(Guid.NewGuid(), pendingRepair.Id, pendingRepair.AssignmentVersion,
                    pendingRepair.Revision, pendingRepair.Version, "Repair completed before the attended appointment"), default)).Status);
        var publication = await database.ListingPublications.AsNoTracking()
            .Where(item => item.VehicleId == listing.Revision.VehicleId).OrderByDescending(item => item.RecordedAt).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(8).DateTime);
        var slots = services.GetRequiredService<AppointmentSchedule>().GetSlots(today, now, TestDriveBooking.Duration).ToArray();
        Assert.True(slots.Length >= 2, "Persisted attendance acceptance requires two remaining same-day booking slots");
        var booked = await services.GetRequiredService<TestDriveBookingService>().BookAsync(customer,
            listing.Revision.VehicleId, new(Guid.NewGuid(), publication.Id, slots[^2].StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, booked.Status);
        var booking = booked.Booking!;
        var assigned = await services.GetRequiredService<TestDriveAssignmentService>().ExecuteAsync(manager,
            booking.Id, WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 0, agent), default);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, assigned.Status);
        var startRequest = new TestDriveStartRequest(Guid.NewGuid(), assigned.Receipt!.Id, true, true);

        Assert.Equal(TestDriveHandlingStatus.Invalid, (await StartAsync(agent,
            startRequest with { DrivingLicenceChecked = false })).Status);
        Assert.Equal(TestDriveHandlingStatus.Forbidden, (await StartAsync(technician, startRequest)).Status);
        Assert.Equal(TestDriveHandlingStatus.Conflict, (await StartAsync(manager, startRequest)).Status);
        Assert.Equal(TestDriveHandlingStatus.Conflict, (await StartAsync(agent,
            startRequest with { AssignmentId = Guid.NewGuid() })).Status);

        var starts = await Task.WhenAll(StartAsync(agent, startRequest), StartAsync(agent, startRequest));
        Assert.All(starts, result => Assert.Equal(TestDriveHandlingStatus.Succeeded, result.Status));
        var start = starts[0].Start!;
        Assert.Equal(JsonSerializer.Serialize(start), JsonSerializer.Serialize(starts[1].Start));
        Assert.Equal(agent, start.StaffId);
        var occupation = await database.TestDriveOccupations.AsNoTracking().SingleAsync(item => item.StartId == start.Id);
        Assert.Equal(booking.VehicleId, occupation.VehicleId);
        Assert.Null(occupation.OutcomeId);
        Assert.Equal(VehicleStatus.Available, await database.Vehicles.Where(item => item.Id == booking.VehicleId)
            .Select(item => item.Status).SingleAsync());
        Assert.Equal(TestDriveHandlingStatus.Unavailable, (await StartAsync(agent,
            startRequest with { OperationId = Guid.NewGuid() })).Status);
        Assert.Equal(TestDriveHandlingStatus.Conflict, (await StartAsync(agent,
            startRequest with { AssignmentId = Guid.NewGuid() })).Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict, (await services.GetRequiredService<TestDriveCancellationService>()
            .CancelByStaffAsync(manager, booking.Id, Guid.NewGuid(), "Customer requested cancellation", default)).Status);
        Assert.Equal(TestDriveAssignmentStatus.Unavailable, (await services.GetRequiredService<TestDriveAssignmentService>()
            .ExecuteAsync(agent, booking.Id, WorkOwnershipKind.Released, new(Guid.NewGuid(), 1), default)).Status);

        var reads = services.GetRequiredService<IStaffTestDriveReadStore>();
        var history = await reads.HistoryAsync(manager, booking.Id, 1, 25, default);
        Assert.Equal(TestDriveReadStatus.Succeeded, history.Status);
        Assert.Equal(["Started", "Assigned", "Booked"], history.Value!.Items.Select(item => item.Kind));
        Assert.Equal(3, history.Value.TotalCount);
        Assert.Equal(agent, history.Value.Items[0].ActorId);
        Assert.Equal(agent, history.Value.Items[1].HandlerId);
        Assert.Equal(customer, history.Value.Items[2].ActorId);
        Assert.All(history.Value.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Actor)));
        Assert.Empty((await reads.HistoryAsync(manager, booking.Id, 2, 25, default)).Value!.Items);
        Assert.Equal(TestDriveReadStatus.Invalid, (await reads.HistoryAsync(manager, booking.Id, 0, 25, default)).Status);
        Assert.Equal(TestDriveReadStatus.NotFound, (await reads.HistoryAsync(manager, Guid.NewGuid(), 1, 25, default)).Status);
        Assert.Equal(TestDriveReadStatus.Forbidden, (await reads.HistoryAsync(customer, booking.Id, 1, 25, default)).Status);
        Assert.Equal(ReservationStatus.NotFound, (await services.GetRequiredService<ReservationService>()
            .ReserveAsync(otherCustomer, booking.VehicleId, new(Guid.NewGuid(), publication.Id, null), default)).Status);

        var draft = await database.InspectionDrafts.AsNoTracking().SingleAsync(item => item.Id == listing.InspectionId);
        Assert.Equal(InspectionAssignmentStatus.Conflict, (await services.GetRequiredService<InspectionReopeningService>()
            .ReopenAsync(manager, draft.Id, new(Guid.NewGuid(), draft.AssignmentVersion, draft.Revision,
                "Inspect a reported concern"), default)).Status);
        var signed = await database.InspectionSignedRevisions.AsNoTracking()
            .SingleAsync(item => item.InspectionId == draft.Id && item.Revision == draft.Revision);
        var latestSave = await database.InspectionDraftSaves.Where(item => item.InspectionId == draft.Id)
            .MaxAsync(item => item.Version);
        var directReopen = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InspectionReopenings" ("Id", "InspectionId", "BranchId", "ActorId", "SignedRevisionId",
                "AssignmentVersion", "ExpectedRevision", "Revision", "AfterSaveVersion", "Reason", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {draft.Id}, {draft.BranchId}, {manager}, {signed.Id}, {draft.AssignmentVersion},
                {draft.Revision}, {draft.Revision + 1}, {latestSave}, 'Inspect a reported concern', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveOccupations_Exclusive", directReopen.ConstraintName);
        Assert.True(await database.InspectionDrafts.Where(item => item.Id == draft.Id).Select(item => item.IsSigned).SingleAsync());
        var forgedRelease = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveOccupations\" SET \"OutcomeId\" = {Guid.NewGuid()} WHERE \"StartId\" = {start.Id}"));
        Assert.Equal("CK_TestDriveOccupations_Immutable", forgedRelease.ConstraintName);
        var cancelled = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveCancellations" ("Id", "BookingId", "CustomerId", "Kind", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {booking.Id}, {customer}, 'CustomerCancelled', clock_timestamp())
            """));
        Assert.Equal("CK_TestDriveCancellations_Handling", cancelled.ConstraintName);

        var next = await services.GetRequiredService<TestDriveBookingService>().BookAsync(otherCustomer, booking.VehicleId,
            new(Guid.NewGuid(), publication.Id, slots[^1].StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, next.Status);
        var nextAssignment = await services.GetRequiredService<TestDriveAssignmentService>().ExecuteAsync(manager,
            next.Booking!.Id, WorkOwnershipKind.Claimed, new(Guid.NewGuid(), 0), default);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, nextAssignment.Status);
        using (var blockedScope = data.Factory.Services.CreateScope())
        {
            var blocked = await blockedScope.ServiceProvider.GetRequiredService<TestDriveHandlingService>()
                .StartAsync(manager, next.Booking.Id, new(Guid.NewGuid(), nextAssignment.Receipt!.Id, true, true), default);
            Assert.Equal(TestDriveHandlingStatus.ScheduleConflict, blocked.Status);
        }

        // A new process reconstructs occupation and immutable receipts without freeing either on restart.
        using (var restarted = fixture.CreateFactory())
        using (var restartedScope = restarted.Services.CreateScope())
        {
            var handler = restartedScope.ServiceProvider.GetRequiredService<TestDriveHandlingService>();
            Assert.Equal(JsonSerializer.Serialize(start), JsonSerializer.Serialize((await handler
                .StartAsync(agent, booking.Id, startRequest, default)).Start));
        }

        var handoff = await services.GetRequiredService<TestDriveAssignmentService>().ExecuteAsync(manager,
            booking.Id, WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 1, manager, "Agent is attending another customer"), default);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, handoff.Status);
        var outcomeRequest = new TestDriveOutcomeRequest(Guid.NewGuid(), handoff.Receipt!.Id,
            TestDriveOutcomeKind.Completed, true, Notes: "Customer reviewed the vehicle");
        Assert.Equal(TestDriveHandlingStatus.Conflict, (await FinishAsync(agent, outcomeRequest)).Status);
        var outcomes = await Task.WhenAll(FinishAsync(manager, outcomeRequest), FinishAsync(manager, outcomeRequest));
        Assert.All(outcomes, result => Assert.Equal(TestDriveHandlingStatus.Succeeded, result.Status));
        var outcome = outcomes[0].Outcome!;
        Assert.Equal(manager, outcome.StaffId);
        Assert.Equal(start.Id, outcome.StartId);
        Assert.Equal(JsonSerializer.Serialize(outcome), JsonSerializer.Serialize(outcomes[1].Outcome));
        Assert.Equal(outcome.Id, await database.TestDriveOccupations.Where(item => item.StartId == start.Id)
            .Select(item => item.OutcomeId).SingleAsync());
        Assert.Equal(agent, (await database.TestDriveStarts.AsNoTracking().SingleAsync(item => item.Id == start.Id)).StaffId);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveOutcomes\" SET \"Notes\" = 'Changed' WHERE \"Id\" = {outcome.Id}"));
        Assert.Equal("CK_TestDriveHandling_Immutable", immutable.ConstraintName);
        // Disposal may only remove the free-text note; any other column change stays refused.
        var rewritten = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveOutcomes\" SET \"Notes\" = NULL, \"CustomerPresent\" = NOT \"CustomerPresent\" WHERE \"Id\" = {outcome.Id}"));
        Assert.Equal("CK_TestDriveHandling_Immutable", rewritten.ConstraintName);
        // Removing only the note is allowed; this probe runs in a transaction that is rolled back.
        await using (var probe = await database.Database.BeginTransactionAsync())
        {
            Assert.Equal(1, await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"TestDriveOutcomes\" SET \"Notes\" = NULL WHERE \"Id\" = {outcome.Id}"));
            await probe.RollbackAsync();
        }

        var newBooking = await services.GetRequiredService<TestDriveBookingService>().BookAsync(customer, booking.VehicleId,
            new(Guid.NewGuid(), publication.Id, slots[^2].StartsAt), default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, newBooking.Status);

        using (var noDriveScope = data.Factory.Services.CreateScope())
        {
            var noDrive = await noDriveScope.ServiceProvider.GetRequiredService<TestDriveHandlingService>().RecordOutcomeAsync(manager,
                next.Booking.Id, new(Guid.NewGuid(), nextAssignment.Receipt!.Id, TestDriveOutcomeKind.NotCompleted, true,
                    "Customer did not bring a valid driving licence"), default);
            Assert.Equal(TestDriveHandlingStatus.Succeeded, noDrive.Status);
            Assert.Null(noDrive.Outcome!.StartId);
            Assert.False(await database.TestDriveOccupations.AnyAsync(item => item.BookingId == next.Booking.Id));
        }

        var raceAssignment = await services.GetRequiredService<TestDriveAssignmentService>().ExecuteAsync(manager,
            newBooking.Booking!.Id, WorkOwnershipKind.Assigned, new(Guid.NewGuid(), 0, agent), default);
        Assert.Equal(TestDriveAssignmentStatus.Succeeded, raceAssignment.Status);
        using (var startScope = data.Factory.Services.CreateScope())
        using (var reservationScope = data.Factory.Services.CreateScope())
        {
            var starting = startScope.ServiceProvider.GetRequiredService<TestDriveHandlingService>().StartAsync(agent,
                newBooking.Booking.Id, new(Guid.NewGuid(), raceAssignment.Receipt!.Id, true, true), default);
            var reserving = reservationScope.ServiceProvider.GetRequiredService<ReservationService>().ReserveAsync(otherCustomer,
                booking.VehicleId, new(Guid.NewGuid(), publication.Id, null), default);
            await Task.WhenAll(starting, reserving);
            Assert.True(starting.Result.Status == TestDriveHandlingStatus.Succeeded && reserving.Result.Status == ReservationStatus.NotFound ||
                starting.Result.Status == TestDriveHandlingStatus.Unavailable && reserving.Result.Status == ReservationStatus.Succeeded);
            var active = await database.TestDriveOccupations.AnyAsync(item => item.BookingId == newBooking.Booking.Id && item.OutcomeId == null);
            Assert.Equal(starting.Result.Status == TestDriveHandlingStatus.Succeeded, active);
            Assert.Equal(reserving.Result.Status == ReservationStatus.Succeeded,
                await database.TestDriveCancellations.AnyAsync(item => item.BookingId == newBooking.Booking.Id));
        }

        await using var maintenance = new DealershipDbContext(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.ConnectionString).Options);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => maintenance.GetService<IMigrator>()
            .MigrateAsync("20261103151704_TestDriveInspectionRepairCancellations"));
        Assert.Contains("Retained test-drive handling evidence", rollback.MessageText, StringComparison.Ordinal);
        // The refused rollback has already undone the later account migrations, so bring the schema back up first.
        await maintenance.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(JsonSerializer.Serialize(outcome), JsonSerializer.Serialize((await FinishAsync(manager, outcomeRequest)).Outcome));

        async Task<TestDriveHandlingResult> StartAsync(Guid actor, TestDriveStartRequest request)
        {
            using var scope = data.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TestDriveHandlingService>()
                .StartAsync(actor, booking.Id, request, default);
        }

        async Task<TestDriveHandlingResult> FinishAsync(Guid actor, TestDriveOutcomeRequest request)
        {
            using var scope = data.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TestDriveHandlingService>()
                .RecordOutcomeAsync(actor, booking.Id, request, default);
        }

        DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
            .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
    }
}
