using Dealership.Infrastructure.TestDrives;
using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationStore(DealershipDbContext database, MailDeliverySignal mail,
    ReservationExpirySignal expiry) : IReservationStore
{
    public async Task<IReservationTransaction> LockAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"reservation:" + request.OperationId}, 0))", cancellationToken);
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {customerId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var profile = await database.CustomerProfiles.FromSqlInterpolated(
                $"SELECT * FROM \"CustomerProfiles\" WHERE \"UserId\" = {customerId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var customer = user is null ? null : new ReservationCustomer(user.IsActive, user.EmailConfirmed, user.Role, user.Email);
            if (user is not { IsActive: true, EmailConfirmed: true, Role: AccountRoles.Customer } || profile is null)
            {
                return new ReservationTransaction(database, transaction, mail, expiry,
                    new(customer, profile, null, null, null, null, false, false, default, null, false));
            }

            var branchId = await database.Vehicles.Where(item => item.Id == vehicleId)
                .Select(item => (Guid?)item.BranchId).SingleOrDefaultAsync(cancellationToken);
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Branches\" WHERE \"Id\" = {branchId} FOR UPDATE", cancellationToken);
            var vehicleIds = await database.Reservations.Where(item => item.CustomerId == customerId && item.IsActive)
                .Select(item => item.VehicleId).ToListAsync(cancellationToken);
            vehicleIds.Add(vehicleId);
            var ids = vehicleIds.Distinct().ToArray();
            var vehicles = await database.Vehicles.FromSqlInterpolated(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            foreach (var vehicle in vehicles)
            {
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT release_unavailable_reservation({vehicle.Id})", cancellationToken);
            }

            var existing = await database.Reservations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.OperationId, cancellationToken);
            var isRelease = await database.ReservationReleases.AnyAsync(item => item.Id == request.OperationId, cancellationToken);
            var listing = await PublicListingQuery.Read(database).Where(row => row.Vehicle.Id == vehicleId &&
                !TestDriveOccupationRows.Active(database).Any(occupation => occupation.VehicleId == row.Vehicle.Id))
                .SingleOrDefaultAsync(cancellationToken);
            var settings = await database.PurchaseSettings.FromSqlInterpolated(
                $"SELECT * FROM \"PurchaseSettings\" WHERE \"Id\" = {PurchaseSettings.SingletonId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var promo = await database.PromoCodes.FromSqlInterpolated(
                $"SELECT * FROM \"PromoCodes\" WHERE \"Id\" = {request.PromoId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var customerHasHold = await database.Reservations.AnyAsync(item => item.CustomerId == customerId && item.IsActive, cancellationToken);
            var vehicleHasHold = await database.Reservations.AnyAsync(item => item.VehicleId == vehicleId && item.IsActive, cancellationToken);
            var now = await database.DatabaseNowAsync(cancellationToken);
            return new ReservationTransaction(database, transaction, mail, expiry, new(customer, profile, listing?.Publication,
                listing?.Revision, settings, promo, customerHasHold, vehicleHasHold, now, existing, isRelease));
        }
        catch
        {
            try
            {
                await transaction.DisposeAsync();
            }
            finally
            {
                database.ChangeTracker.Clear();
            }

            throw;
        }
    }
}
