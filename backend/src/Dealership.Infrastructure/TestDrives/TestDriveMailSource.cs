using Dealership.Domain.Common;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveMailSource(DealershipDbContext database, TimeProvider clock) : IMailDeliverySource
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.TestDriveMailOutbox.FromSqlInterpolated($"""
            SELECT m.* FROM "TestDriveMailOutbox" m
            WHERE m."DeliveredAt" IS NULL AND m."NextDeliveryAt" <= {now}
              AND (m."DeliveryClaimExpiresAt" IS NULL OR m."DeliveryClaimExpiresAt" <= {now})
              AND (m."Kind" <> 'Confirmation' OR NOT EXISTS (
                  SELECT 1 FROM "TestDriveCancellations" c WHERE c."BookingId" = m."BookingId"))
            ORDER BY m."NextDeliveryAt", m."Id" LIMIT 1 FOR UPDATE OF m SKIP LOCKED
            """);
        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now, ComposeAsync, cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.TestDriveMailOutbox, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private async Task<OutboundMail> ComposeAsync(TestDriveMail message, CancellationToken cancellationToken)
    {
        var booking = await database.TestDriveBookings.AsNoTracking()
            .SingleAsync(item => item.Id == message.BookingId, cancellationToken);
        var branch = await database.Branches.AsNoTracking()
            .SingleAsync(item => item.Id == booking.BranchId, cancellationToken);
        var vehicle = await database.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == booking.VehicleId, cancellationToken);
        var startsAt = TimeZoneInfo.ConvertTime(booking.StartsAt, Malaysia);
        var state = message.Kind == TestDriveMailKind.Cancellation ? "cancelled" : "confirmed";
        var body = FormattableString.Invariant($"Your ApexAuto test drive is {state}.\nReference: {booking.Id:D}\nBranch: {branch.Name}\nVehicle: {vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)");
        return new(message.RecipientEmail, $"ApexAuto test drive {state}", body);
    }
}
