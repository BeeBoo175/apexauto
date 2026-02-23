using Dealership.Infrastructure.Intake;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class AppointmentCheckInTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task CanonicalCommandSerializesReplayPreservesEvidenceAndRejectsConflictingVehicles()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync();
        var evidence = CheckInTestData.Evidence with { Vin = " pl1bt65r84b123456 ", Notes = " Owner present " };
        var concurrent = await Task.WhenAll(CheckInAsync(data.Factory, data.StaffId, appointment.Id, evidence),
            CheckInAsync(data.Factory, data.StaffId, appointment.Id, evidence));
        Assert.All(concurrent, result => Assert.Equal(CheckInStatus.Succeeded, result.Status));
        Assert.Equal(concurrent[0].Receipt, concurrent[1].Receipt);
        var receipt = concurrent[0].Receipt!;
        Assert.Equal(CheckInTestData.Evidence, receipt.Evidence);
        Assert.Equal(data.StaffId, receipt.StaffId);
        Assert.Equal(CheckInStatus.Conflict,
            (await CheckInAsync(data.Factory, data.StaffId, appointment.Id, evidence with { MileageKilometres = 1 })).Status);

        using var restarted = fixture.CreateFactory(clock: new NextDayClock());
        Assert.Equal(receipt, (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Receipt);
        using var scope = data.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var vehicle = await database.Vehicles.SingleAsync();
        Assert.Equal(receipt.VehicleId, vehicle.Id);
        Assert.Equal(VehicleStatus.PendingInspection, vehicle.Status);
        Assert.Equal(92000, vehicle.MileageKilometres);
        Assert.Equal(90000, (await database.IntakeAppointments.SingleAsync()).Valuation.MileageKilometres);
        Assert.Single(await database.AppointmentCheckIns.ToArrayAsync());
        Assert.Equal(data.CustomerEmail, (await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.CheckIn).SingleAsync()).RecipientEmail);
        Assert.Equal(CancellationStatus.CheckedIn,
            (await scope.ServiceProvider.GetRequiredService<IAppointmentCancellationStore>()
                .CancelAsync(data.OwnerId, appointment.Id, CancellationToken.None)).Status);

        var duplicateVin = await data.BookAsync(1, "BPP 1234");
        Assert.Equal(CheckInStatus.VehicleConflict,
            (await CheckInAsync(data.Factory, data.StaffId, duplicateVin.Id, evidence)).Status);
        var duplicatePlate = await data.BookAsync(2, "WYY8821");
        Assert.Equal(CheckInStatus.VehicleConflict,
            (await CheckInAsync(data.Factory, data.StaffId, duplicatePlate.Id,
                evidence with { Vin = "PL1BT65R84B123457" })).Status);
        Assert.Equal(1, await database.Vehicles.CountAsync());
        Assert.Equal(1, await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.CheckIn).CountAsync());

        await database.Users.Where(user => user.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false));
        Assert.Equal(receipt, (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Receipt);
        Assert.Equal(CheckInStatus.Unavailable,
            (await CheckInAsync(data.Factory, data.StaffId, duplicateVin.Id, evidence)).Status);

        foreach (var role in new[] { AccountRoles.Customer, AccountRoles.WorkshopTechnician,
            AccountRoles.GeneralManager, AccountRoles.Administrator })
        {
            // Respect the existing account shape constraint while testing current persisted role authorization.
            var branch = role is AccountRoles.WorkshopTechnician ? (Guid?)data.BranchId : null;
            await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
                .SetProperty(user => user.Role, role).SetProperty(user => user.BranchId, branch));
            Assert.Equal(CheckInStatus.Forbidden,
                (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Status);
        }

        await database.Users.Where(user => user.Id == data.StaffId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.Role, AccountRoles.BranchManager).SetProperty(user => user.BranchId, data.BranchId));
        Assert.Equal(receipt, (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Receipt);
        var colleagueId = Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new StaffProvisioningRequest(colleagueId, $"farid.hakim.{Guid.NewGuid():N}@example.invalid", "Password123!",
                AccountRoles.SalesAgent, data.BranchId), CancellationToken.None);
        Assert.Equal(receipt, (await CheckInAsync(restarted, colleagueId, appointment.Id, evidence)).Receipt);
        var otherBranchId = Guid.NewGuid();
        await scope.ServiceProvider.GetRequiredService<BranchProvisioningService>().ProvisionBranchAsync(
            DevelopmentFixtureManifest.Branches[0] with { Id = otherBranchId, Name = "ApexAuto Petaling Jaya" }, CancellationToken.None);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, otherBranchId));
        Assert.Equal(CheckInStatus.NotFound,
            (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Status);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.BranchId, data.BranchId));
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, false));
        Assert.Equal(CheckInStatus.Forbidden,
            (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Status);
        await database.Branches.Where(branch => branch.Id == data.BranchId)
            .ExecuteUpdateAsync(update => update.SetProperty(branch => branch.IsActive, true));
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(CheckInStatus.Forbidden,
            (await CheckInAsync(restarted, data.StaffId, appointment.Id, evidence)).Status);
        await database.Users.Where(user => user.Id == data.StaffId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        await database.Users.Where(user => user.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, true));
        await AssertUnavailableLeavesNoBundleAsync(data);
    }

    private async Task AssertUnavailableLeavesNoBundleAsync(CheckInTestData data)
    {
        var appointment = await data.BookAsync(3, "VCA 6271");
        var evidence = CheckInTestData.Evidence with { Vin = "PL1BT65R84B123458" };
        Assert.Equal(CheckInStatus.Invalid, (await CheckInAsync(data.Factory, data.StaffId, appointment.Id,
            evidence with { CustomerPresent = false })).Status);
        using var tomorrow = fixture.CreateFactory(clock: new NextDayClock());
        Assert.Equal(CheckInStatus.Unavailable,
            (await CheckInAsync(tomorrow, data.StaffId, appointment.Id, evidence)).Status);
        using var scope = data.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        await database.Users.Where(user => user.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(CheckInStatus.Unavailable,
            (await CheckInAsync(data.Factory, data.StaffId, appointment.Id, evidence)).Status);
        await database.Users.Where(user => user.Id == data.OwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, true));
        var cancellable = await data.BookAsync(0, "VCA 6272", dateOffset: 1);
        Assert.Equal(CancellationStatus.Succeeded,
            (await scope.ServiceProvider.GetRequiredService<IAppointmentCancellationStore>()
                .CancelAsync(data.OwnerId, cancellable.Id, CancellationToken.None)).Status);
        Assert.Equal(CheckInStatus.Unavailable,
            (await CheckInAsync(tomorrow, data.StaffId, cancellable.Id, evidence)).Status);
        Assert.False(await database.AppointmentCheckIns.AnyAsync(item => item.AppointmentId == appointment.Id));
        Assert.False(await database.Vehicles.AnyAsync(item => item.IntakeAppointmentId == appointment.Id));
        Assert.False(await database.AppointmentMailOutbox.AnyAsync(item => item.Kind == AppointmentMailKind.CheckIn && item.AppointmentId == appointment.Id));
    }

    private static async Task<CheckInResult> CheckInAsync(WebApplicationFactory<Program> factory, Guid staffId,
        Guid appointmentId, CheckInEvidence evidence)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppointmentCheckInService>()
            .CheckInAsync(staffId, appointmentId, evidence, CancellationToken.None);
    }

    private sealed class NextDayClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(1);
    }
}
