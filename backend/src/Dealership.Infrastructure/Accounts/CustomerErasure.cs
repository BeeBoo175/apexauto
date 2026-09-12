using System.Security.Cryptography;
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
        // Pictures first: the removal trigger needs the picture row saved as removed before its receipt.
        var pictures = await database.AccountPictures.FromSqlInterpolated(
            $"SELECT * FROM \"AccountPictures\" WHERE \"UserId\" = {userId} AND \"RemovedAt\" IS NULL ORDER BY (\"AvailableAt\" IS NULL), \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        foreach (var picture in pictures)
        {
            if (picture.AvailableAt is null)
            {
                picture.ConfirmAvailable(now);
            }

            await AccountPictureRemovals.RecordAsync(database, picture, AccountPictureRemovalReason.Removed, now,
                cancellationToken);
        }

        // A random stamp, never NULL, because NULL stamps compare equal.
        var stamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await database.Users.Where(user => user.Id == userId).ExecuteUpdateAsync(update => update
            .SetProperty(user => user.ClosedAt, now)
            .SetProperty(user => user.IsActive, false)
            .SetProperty(user => user.SuspendedAt, (DateTimeOffset?)null)
            .SetProperty(user => user.PasswordHash, (string?)null)
            .SetProperty(user => user.EmailConfirmed, false)
            .SetProperty(user => user.Email, (string?)null)
            .SetProperty(user => user.NormalizedEmail, (string?)null)
            .SetProperty(user => user.UserName, (string?)null)
            .SetProperty(user => user.NormalizedUserName, (string?)null)
            .SetProperty(user => user.AccountUsername, (string?)null)
            .SetProperty(user => user.PhoneNumber, (string?)null)
            .SetProperty(user => user.PhoneNumberConfirmed, false)
            .SetProperty(user => user.SecurityStamp, stamp), cancellationToken);

        await AccountAccessRevocation.RevokeAsync(database, userId, now, cancellationToken);
        await database.EmailVerificationChallenges
            .Where(challenge => challenge.UserId == userId && challenge.InvalidatedAt == null &&
                challenge.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(challenge => challenge.InvalidatedAt, now),
                cancellationToken);
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
        database.AccountEvents.Add(new AccountEvent(operationId, userId, actorId, AccountEventKind.Erased, reason, null,
            now));
        await database.SaveChangesAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AccountEvents" SET "Reason" = NULL, "Changes" = NULL, "MinimizedAt" = {now}
            WHERE "TargetId" = {userId} AND "MinimizedAt" IS NULL AND ("Reason" IS NOT NULL OR "Changes" IS NOT NULL)
            """, cancellationToken);
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
    // shared cleanup worker; attached evidence of appointments that took place is retained. Pictures whose objects
    // are already gone drop their hash now, the rest when the worker confirms deletion.
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
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AccountPictures" p SET "Sha256" = NULL
            WHERE p."UserId" = {userId} AND p."Sha256" IS NOT NULL AND EXISTS
                (SELECT 1 FROM "AccountPictureRemovals" r WHERE r."PictureId" = p."Id" AND r."DeletedAt" IS NOT NULL)
            """, cancellationToken);
    }

    // Rows are matched by id through their owner, never by the address, which is released for a new account.
    private static async Task RemoveRecipientsAsync(DealershipDbContext database, Guid userId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "EmailVerificationChallenges" SET "RecipientEmail" = NULL
            WHERE "UserId" = {userId} AND "RecipientEmail" IS NOT NULL
            """, cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "PasswordResetChallenges" SET "RecipientEmail" = NULL
            WHERE "UserId" = {userId} AND "RecipientEmail" IS NOT NULL
            """, cancellationToken);
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
