using Dealership.Domain.Accounts;
using Dealership.Domain.Customers;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// Every database change of a customer erasure, run by the caller inside its locked transaction. The identity closes
// first because the minimization guards of the dependent tables are bound to the closed state of the owner.
internal static class CustomerErasure
{
    public static async Task ApplyAsync(DealershipDbContext database, Guid userId, CustomerProfile profile,
        Guid operationId, Guid actorId, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await AccountErasure.CloseIdentityAsync(database, userId, now, cancellationToken);
        await RemoveRecipientsAsync(database, userId, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Reservations" r SET "BuyerEmail" = NULL, "BuyerMobile" = NULL,
                "BuyerNric" = CASE WHEN EXISTS (SELECT 1 FROM "SaleDocuments" d WHERE d."SalesOrderId" = r."Id")
                    THEN r."BuyerNric" ELSE NULL END
            WHERE r."CustomerId" = {userId} AND NOT r."IsActive" AND
                (r."BuyerEmail" IS NOT NULL OR r."BuyerMobile" IS NOT NULL OR r."BuyerNric" IS NOT NULL)
            """, cancellationToken);

        await RemoveUploadsAsync(database, userId, now, cancellationToken);
        profile.Erase(now);
        await AccountErasure.RecordAsync(database, userId, operationId, actorId, reason, now, cancellationToken);
    }

    // True while any picture of the user still waits for its object to be deleted.
    public static async Task<bool> FileRemovalPendingAsync(DealershipDbContext database, Guid userId,
        CancellationToken cancellationToken)
    {
        return await database.AccountPictureRemovals.AsNoTracking().AnyAsync(removal =>
            removal.DeletedAt == null && database.AccountPictures.Any(picture => picture.Id == removal.PictureId &&
                picture.UserId == userId), cancellationToken);
    }

    // Unattached intake uploads and photographs of appointments that never happened get a removal receipt for the
    // shared cleanup worker; attached evidence of appointments that took place is retained.
    private static async Task RemoveUploadsAsync(DealershipDbContext database, Guid userId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PhotoRemovals" ("PhotoId", "Reason", "RequestedAt", "EligibleAt", "Attempts", "NextAttemptAt")
            SELECT p."Id", 'OwnerErased', {now}, {now}, 0, {now}
            FROM "IntakePhotos" p
            WHERE p."OwnerId" = {userId} AND NOT EXISTS (SELECT 1 FROM "PhotoRemovals" r WHERE r."PhotoId" = p."Id") AND
                (NOT EXISTS (SELECT 1 FROM "AppointmentPhotos" l WHERE l."PhotoId" = p."Id") OR EXISTS
                    (SELECT 1 FROM "AppointmentPhotos" l JOIN "IntakeAppointments" a ON a."Id" = l."AppointmentId"
                     WHERE l."PhotoId" = p."Id" AND a."CancelledAt" IS NULL AND a."EndsAt" < {now} AND
                        NOT EXISTS (SELECT 1 FROM "AppointmentCheckIns" c WHERE c."AppointmentId" = a."Id")))
            """, cancellationToken);
    }

    // Rows are matched by id through their owner, never by the address, which is released for a new account.
    private static async Task RemoveRecipientsAsync(DealershipDbContext database, Guid userId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AppointmentMailOutbox" m SET "RecipientEmail" = NULL FROM "IntakeAppointments" a
            WHERE a."Id" = m."AppointmentId" AND a."OwnerId" = {userId} AND m."RecipientEmail" IS NOT NULL
            """, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "TestDriveMailOutbox" m SET "RecipientEmail" = NULL FROM "TestDriveBookings" b
            WHERE b."Id" = m."BookingId" AND b."CustomerId" = {userId} AND m."RecipientEmail" IS NOT NULL
            """, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "SaleDeliveryMailOutbox" m SET "RecipientEmail" = NULL FROM "Reservations" r
            WHERE r."Id" = m."SalesOrderId" AND r."CustomerId" = {userId} AND m."RecipientEmail" IS NOT NULL
            """, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ReservationDepositMailOutbox" m SET "RecipientEmail" = NULL FROM "ReservationDepositRequests" q
            WHERE q."Id" = m."RequestId" AND q."CustomerId" = {userId} AND m."RecipientEmail" IS NOT NULL
            """, cancellationToken);
    }
}
