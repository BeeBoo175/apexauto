using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixtureMail(DealershipDbContext database)
{
    private Guid[] _existingAppointments = [];
    private Guid[] _existingReservations = [];
    private Guid[] _existingDeliveries = [];
    private Guid[] _existingTestDrives = [];

    public async Task CaptureAsync(CancellationToken cancellationToken)
    {
        _existingAppointments = await database.AppointmentMailOutbox.Select(item => item.Id).ToArrayAsync(cancellationToken);
        _existingDeliveries = await database.SaleDeliveryMailOutbox.Select(item => item.Id).ToArrayAsync(cancellationToken);
        _existingReservations = await database.ReservationMailOutbox.Select(item => item.Id).ToArrayAsync(cancellationToken);
        _existingTestDrives = await database.TestDriveMailOutbox.Select(item => item.Id).ToArrayAsync(cancellationToken);
    }

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        var appointments = DevelopmentIntakeManifest.Appointments.Select(item => item.Id).ToArray();
        var cancellations = DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.Cancelled)
            .Select(item => item.Id).ToArray();
        var checkIns = DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.CheckedIn)
            .Select(item => item.Id).ToArray();
        var reservations = DevelopmentReservationScenarios.All.Select(item => item.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var testDrives = DevelopmentTestDriveFixtures.Scenarios.Select(item => item.OperationId("test-drive-booking")).ToArray();
        await database.TestDriveMailOutbox.Where(item => testDrives.Contains(item.BookingId) &&
            !_existingTestDrives.Contains(item.Id) && item.DeliveredAt == null && item.DeliveryClaimId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, now), cancellationToken);
        // Only new canonical fixture intents belong to this run; manual actions and older pending mail remain untouched.
        await database.AppointmentMailOutbox.Where(item =>
            (item.Kind == AppointmentMailKind.Confirmation && appointments.Contains(item.AppointmentId) ||
             item.Kind == AppointmentMailKind.Cancellation && cancellations.Contains(item.AppointmentId) ||
             item.Kind == AppointmentMailKind.CheckIn && checkIns.Contains(item.AppointmentId)) &&
            !_existingAppointments.Contains(item.Id) && item.DeliveredAt == null && item.DeliveryClaimId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, now), cancellationToken);
        await database.ReservationMailOutbox.Where(item => reservations.Contains(item.ReservationId) &&
            !_existingReservations.Contains(item.Id) && item.DeliveredAt == null && item.DeliveryClaimId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, now), cancellationToken);
        var deliveries = DevelopmentSaleScenarios.All.Select(item => item.ReservationId).ToArray();
        await database.SaleDeliveryMailOutbox.Where(item => deliveries.Contains(item.SalesOrderId) &&
            !_existingDeliveries.Contains(item.Id) && item.DeliveredAt == null && item.DeliveryClaimId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DeliveredAt, now), cancellationToken);
    }
}
