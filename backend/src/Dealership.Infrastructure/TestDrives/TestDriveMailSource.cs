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
            SELECT * FROM "TestDriveMailOutbox"
            WHERE "DeliveredAt" IS NULL AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
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
        var body = FormattableString.Invariant($"Your ApexAuto test drive is confirmed.\nReference: {booking.Id:D}\nBranch: {branch.Name}\nVehicle: {vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)");
        return new(message.RecipientEmail, "ApexAuto test drive confirmed", body);
    }
}
