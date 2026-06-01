using System.Text.Json;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentTestDriveFixtureAssertions
{
    internal static async Task<string> SnapshotAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var bookings = await database.TestDriveBookings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var mail = await database.TestDriveMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var cancellations = await database.TestDriveCancellations.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var assignments = await database.TestDriveAssignments.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(DevelopmentTestDriveFixtures.Scenarios.Count + 4, bookings.Length);
        Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.CustomerCancelled);
        var staffCancellation = Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.StaffCancelled);
        Assert.NotNull(staffCancellation.StaffId);
        Assert.NotNull(staffCancellation.Reason);
        var systemCancellation = Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.VehicleReserved);
        Assert.Null(systemCancellation.StaffId);
        Assert.NotNull(systemCancellation.ReservationId);
        Assert.Equal("Vehicle reserved", systemCancellation.Reason);
        var withdrawal = Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.ListingWithdrawn);
        Assert.Null(withdrawal.StaffId);
        Assert.Null(withdrawal.ReservationId);
        Assert.Equal(DevelopmentTestDriveWithdrawalFixtures.WithdrawalId, withdrawal.ListingWithdrawalId);
        Assert.Equal("Listing withdrawn", withdrawal.Reason);
        var reopening = Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.InspectionReopened);
        Assert.Equal(DevelopmentTestDriveReopeningFixtures.ReopeningId, reopening.InspectionReopeningId);
        Assert.Null(reopening.StaffId);
        Assert.Null(reopening.ReservationId);
        Assert.Null(reopening.ListingWithdrawalId);
        Assert.Equal("Vehicle requires reinspection", reopening.Reason);
        var repair = Assert.Single(cancellations, item => item.Kind == TestDriveCancellationKind.InspectionRepairRequested);
        Assert.Equal(DevelopmentTestDriveRepairFixtures.RequestId, repair.InspectionRepairRequestId);
        Assert.Null(repair.StaffId);
        Assert.Null(repair.ReservationId);
        Assert.Null(repair.ListingWithdrawalId);
        Assert.Null(repair.InspectionReopeningId);
        Assert.Equal("Vehicle requires repairs", repair.Reason);
        var assignment = Assert.Single(assignments);
        Assert.Contains(bookings, booking => booking.Id == assignment.BookingId && booking.BranchId == assignment.BranchId);
        Assert.Equal(bookings.Length + cancellations.Length, mail.Length);
        Assert.Equal(DevelopmentTestDriveFixtures.Scenarios.Count, bookings.Select(item => item.CustomerId).Distinct().Count());
        Assert.All(mail, message =>
        {
            Assert.Contains(bookings, booking => booking.Id == message.BookingId);
            Assert.NotNull(message.DeliveredAt);
            Assert.Equal(0, message.DeliveryAttempts);
        });
        return JsonSerializer.Serialize(new { Bookings = bookings, Cancellations = cancellations, Assignments = assignments, Mail = mail });
    }
}
