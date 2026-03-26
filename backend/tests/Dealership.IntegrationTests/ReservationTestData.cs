using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class ReservationTestData
{
    internal static async Task<Guid> RegisterCustomerAsync(IServiceProvider services, DealershipDbContext database)
    {
        var email = $"hafiz.{Guid.NewGuid():N}@example.invalid";
        var registration = new RegistrationRequest(Guid.NewGuid(), "Hafiz Rahman", email, "Password123!",
            $"920418{Random.Shared.Next(100000, 1000000)}", "0135678910", "20 Jalan Mawar, Shah Alam");
        Assert.Equal(RegistrationStatus.Succeeded, (await services.GetRequiredService<RegistrationService>()
            .RegisterAsync(registration, CancellationToken.None)).Status);
        var customerId = await database.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
        var challenge = await database.EmailVerificationChallenges.SingleAsync(item => item.UserId == customerId);
        var token = services.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(VerificationStatus.Accepted, (await services.GetRequiredService<EmailVerificationService>()
            .ConfirmAsync(token, CancellationToken.None)).Status);
        return customerId;
    }

    internal static async Task<Guid> ProvisionAsync(IServiceProvider services, Guid? branchId, string role)
    {
        var id = Guid.NewGuid();
        await services.GetRequiredService<StaffProvisioningService>().ProvisionStaffAsync(
            new(id, $"staff.{id:N}@example.invalid", "Password123!", role, branchId), CancellationToken.None);
        return id;
    }

    internal static async Task<ListingRecord> CreateListingAsync(CheckInTestData data, IServiceProvider services,
        Guid manager, Guid technician, int index)
    {
        var appointment = await data.BookAsync(index, $"WYY {8800 + index}");
        var checkIn = await services.GetRequiredService<AppointmentCheckInService>().CheckInAsync(data.StaffId,
            appointment.Id, CheckInTestData.Evidence with { Vin = $"PL1BT{index:D12}" }, CancellationToken.None);
        Assert.Equal(CheckInStatus.Succeeded, checkIn.Status);
        var vehicleId = checkIn.Receipt!.VehicleId;
        var assignment = await services.GetRequiredService<InspectionAssignmentService>().AssignAsync(technician,
            vehicleId, new(Guid.NewGuid(), technician, 0, null), CancellationToken.None);
        var inspectionId = assignment.Receipt!.InspectionId;
        var saved = await services.GetRequiredService<InspectionDraftSaveService>().SaveAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, 0, []) { ExaminedPointCodes = [.. InspectionCatalog.Points.Select(point => point.Code)] },
            CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, saved.Status);
        var signed = await services.GetRequiredService<InspectionSignoffService>().SignAsync(technician, inspectionId,
            new(Guid.NewGuid(), 1, 1, 1), CancellationToken.None);
        Assert.Equal(InspectionAssignmentStatus.Succeeded, signed.Status);
        var listings = services.GetRequiredService<ListingService>();
        var draft = await listings.SaveAsync(manager, vehicleId, new(Guid.NewGuid(), 0, 60000m, 90m,
            VehicleTransmission.Automatic, VehicleFuel.Petrol, "Inspected Malaysian vehicle"), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, draft.Status);
        var published = await listings.PublishAsync(manager, vehicleId,
            new(Guid.NewGuid(), draft.Draft!.Id, signed.Report!.OperationId), CancellationToken.None);
        Assert.Equal(ListingStatus.Succeeded, published.Status);
        return new(inspectionId, draft.Draft, published.Publication!);
    }

    internal sealed record ListingRecord(Guid InspectionId, ListingRevision Revision, ListingPublication Publication);
}
