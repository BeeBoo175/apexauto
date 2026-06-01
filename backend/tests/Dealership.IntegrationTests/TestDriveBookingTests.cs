using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class TestDriveBookingTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task AdmissionSerializesCapacityAndCustomerLimitAndRetainsExactReceiptAndMail()
    {
        await TestDriveAssignmentAssertions.VerifyEmptyMigrationAsync(fixture);
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var scope = data.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        await using var database = NewContext();
        var other = await RegisterCustomerAsync(services, database);
        var third = await RegisterCustomerAsync(services, database);
        var now = DateTimeOffset.UtcNow;
        var tomorrow = DateOnly.FromDateTime(now.AddHours(8).DateTime).AddDays(1);
        var schedule = services.GetRequiredService<InspectionSchedule>();
        var slots = Enumerable.Range(0, 7).SelectMany(day => schedule.GetSlots(tomorrow.AddDays(day), now,
            TestDriveBooking.Duration)).ToArray();
        var vehicleId = listing.Revision.VehicleId;
        var request = new TestDriveBookingRequest(Guid.NewGuid(), listing.Publication.Id, slots[0].StartsAt);

        Assert.Equal(TestDriveBookingStatus.Invalid, (await BookAsync(data.OwnerId,
            request with { OperationId = Guid.Empty })).Status);
        Assert.Equal(TestDriveBookingStatus.Invalid, (await BookAsync(data.OwnerId,
            request with { StartsAt = DateTimeOffset.MaxValue })).Status);
        Assert.Equal(TestDriveBookingStatus.Forbidden, (await BookAsync(manager, request)).Status);
        await database.Users.Where(item => item.Id == other)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, false));
        Assert.Equal(TestDriveBookingStatus.Forbidden, (await BookAsync(other, request)).Status);
        await database.Users.Where(item => item.Id == other)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.EmailConfirmed, true));
        Assert.Equal(TestDriveBookingStatus.VehicleUnavailable, (await BookAsync(data.OwnerId,
            request with { PublicationId = Guid.NewGuid() })).Status);
        Assert.Equal(TestDriveBookingStatus.SlotUnavailable, (await BookAsync(data.OwnerId,
            request with { StartsAt = slots[0].StartsAt.AddMinutes(1) })).Status);

        var repeats = await Task.WhenAll(BookAsync(data.OwnerId, request), BookAsync(data.OwnerId, request));
        Assert.All(repeats, result => Assert.Equal(TestDriveBookingStatus.Succeeded, result.Status));
        var receipt = repeats[0].Booking!;
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(repeats[1].Booking));
        Assert.Equal(TestDriveBooking.Duration, receipt.EndsAt - receipt.StartsAt);
        Assert.Equal(TestDriveBookingStatus.Conflict, (await BookAsync(other, request)).Status);
        Assert.Equal(TestDriveBookingStatus.Conflict, (await BookAsync(data.OwnerId,
            request with { StartsAt = slots[1].StartsAt })).Status);
        Assert.Equal(TestDriveBookingStatus.CustomerHasBooking, (await BookAsync(data.OwnerId,
            request with { OperationId = Guid.NewGuid(), StartsAt = slots[1].StartsAt })).Status);
        Assert.Equal(TestDriveBookingStatus.SlotUnavailable, (await BookAsync(other,
            request with { OperationId = Guid.NewGuid() })).Status);

        var next = request with { OperationId = Guid.NewGuid(), StartsAt = slots[1].StartsAt };
        var race = await Task.WhenAll(BookAsync(other, next), BookAsync(third, next with { OperationId = Guid.NewGuid() }));
        Assert.Single(race, result => result.Status == TestDriveBookingStatus.Succeeded);
        Assert.Single(race, result => result.Status == TestDriveBookingStatus.SlotUnavailable);
        Assert.Equal(2, await database.TestDriveBookings.CountAsync());
        Assert.Equal(2, await database.TestDriveMailOutbox.CountAsync());

        var immutable = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveBookings\" SET \"StartsAt\" = \"StartsAt\" WHERE \"Id\" = {receipt.Id}"));
        Assert.Equal("CK_TestDriveBookings_Immutable", immutable.ConstraintName);
        var mail = await database.TestDriveMailOutbox.AsNoTracking().SingleAsync(item => item.BookingId == receipt.Id);
        var changedMail = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TestDriveMailOutbox\" SET \"RecipientEmail\" = {"other@example.invalid"} WHERE \"Id\" = {mail.Id}"));
        Assert.Equal("CK_TestDriveMailOutbox_Immutable", changedMail.ConstraintName);
        var loser = race[0].Status == TestDriveBookingStatus.Succeeded ? third : other;
        var customerLimit = await Assert.ThrowsAsync<PostgresException>(() => InsertWithoutMailAsync(data.OwnerId, slots[2]));
        Assert.Equal("CK_TestDriveBookings_CustomerLimit", customerLimit.ConstraintName);
        var capacity = await Assert.ThrowsAsync<PostgresException>(() => InsertWithoutMailAsync(loser, slots[0]));
        Assert.Equal("EX_TestDriveBookings_Slot", capacity.ConstraintName);
        var missingMail = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestDriveBookings" ("Id", "CustomerId", "VehicleId", "BranchId", "PublicationId", "StartsAt", "EndsAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {loser}, {vehicleId}, {data.BranchId}, {listing.Publication.Id},
                {slots[2].StartsAt}, {slots[2].EndsAt}, {DateTimeOffset.UtcNow})
            """));
        Assert.Equal("CK_TestDriveBookings_Confirmation", missingMail.ConstraintName);
        Assert.Equal(2, await database.TestDriveBookings.CountAsync());

        var source = new TestDriveMailSource(database, TimeProvider.System);
        var claimed = await source.ClaimNextAsync(default);
        Assert.NotNull(claimed);
        Assert.Contains("Asia/Kuala_Lumpur", claimed.Message.Body);
        await source.MarkDeliveredAsync(claimed.Id, Guid.NewGuid(), default);
        Assert.Null(await database.TestDriveMailOutbox.Where(item => item.Id == claimed.Id)
            .Select(item => item.DeliveredAt).SingleAsync());
        await source.MarkDeliveredAsync(claimed.Id, claimed.ClaimId, default);
        Assert.NotNull(await database.TestDriveMailOutbox.Where(item => item.Id == claimed.Id)
            .Select(item => item.DeliveredAt).SingleAsync());

        using var restarted = fixture.CreateFactory();
        using var restartedScope = restarted.Services.CreateScope();
        var replay = await restartedScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
            .BookAsync(data.OwnerId, vehicleId, request, default);
        Assert.Equal(TestDriveBookingStatus.Succeeded, replay.Status);
        Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(replay.Booking));

        await TestDriveAssignmentAssertions.VerifyAsync(data, fixture, database, receipt, manager, technician);
        await TestDriveCancellationAssertions.VerifyAsync(data.Factory, fixture, database, receipt, request, other, manager);
        using var terminalScope = data.Factory.Services.CreateScope();
        var terminal = await terminalScope.ServiceProvider.GetRequiredService<TestDriveAssignmentService>()
            .ExecuteAsync(manager, receipt.Id, TestDriveAssignmentKind.Released,
                new(Guid.NewGuid(), await database.TestDriveAssignments.Where(item => item.BookingId == receipt.Id)
                    .MaxAsync(item => item.Version)), default);
        Assert.Equal(TestDriveAssignmentStatus.Unavailable, terminal.Status);

        await TestDriveWriterLockAssertions.VerifyAsync(data, fixture, listing, manager, technician);
        await TestDriveHandlingAssertions.VerifyAsync(data, fixture, listing, manager, technician);

        async Task<TestDriveBookingResult> BookAsync(Guid customer, TestDriveBookingRequest command)
        {
            using var commandScope = data.Factory.Services.CreateScope();
            return await commandScope.ServiceProvider.GetRequiredService<TestDriveBookingService>()
                .BookAsync(customer, vehicleId, command, default);
        }

        Task<int> InsertWithoutMailAsync(Guid customer, InspectionSlot slot)
        {
            return database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TestDriveBookings" ("Id", "CustomerId", "VehicleId", "BranchId", "PublicationId", "StartsAt", "EndsAt", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {customer}, {vehicleId}, {data.BranchId}, {listing.Publication.Id},
                    {slot.StartsAt}, {slot.EndsAt}, {DateTimeOffset.UtcNow})
                """);
        }
    }

    private DealershipDbContext NewContext() => new(new DbContextOptionsBuilder<DealershipDbContext>()
        .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);
}
