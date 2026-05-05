using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dealership.Infrastructure.Sales;

internal sealed class SaleCommandTransaction(DealershipDbContext database, IDbContextTransaction transaction,
    SaleCommandContext context) : ISaleCommandTransaction
{
    public SaleCommandContext Context => context;

    private SalesOrder Sale => context.Sale ?? throw new InvalidOperationException("No sale is locked.");

    public Task<SaleStaff?> LockStaffAsync(Guid staffId, CancellationToken cancellationToken) =>
        SaleStaffLocks.LockAsync(database, staffId, cancellationToken);

    public Task<PurchaseSettings?> LockSettingsAsync(CancellationToken cancellationToken) =>
        database.PurchaseSettings.FromSqlInterpolated($"SELECT * FROM \"PurchaseSettings\" WHERE \"Id\" = 1 FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    // Only an active package the grade signed for this reservation's listing qualifies for can be sold.
    public async Task<DealWarranty?> ResolveWarrantyAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var package = await database.WarrantyPackages.FromSqlInterpolated(
            $"SELECT * FROM \"WarrantyPackages\" WHERE \"Id\" = {packageId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (package is null)
        {
            return null;
        }

        var grade = await SaleGrades.SignedGradeAsync(database, Sale.Id, cancellationToken);
        return package.IsEligibleFor(grade) ? new DealWarranty(package.Id, package.Name, package.Price) : null;
    }

    public async Task<SaleTradeInChoice?> ResolveTradeInAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await database.IntakeAppointments.FromSqlInterpolated(
            $"SELECT * FROM \"IntakeAppointments\" WHERE \"Id\" = {appointmentId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (appointment is null || appointment.OwnerId != Sale.CustomerId || appointment.CancelledAt is not null)
        {
            return null;
        }

        var taken = await database.SalesOrders.AnyAsync(item => item.Id != Sale.Id &&
            item.TradeInAppointmentId == appointmentId &&
            item.Status != SalesOrderStatus.Expired && item.Status != SalesOrderStatus.Cancelled, cancellationToken);
        return new SaleTradeInChoice(appointment.Id, SaleLabels.TradeIn(appointment), appointment.Valuation.Maximum,
            taken);
    }

    public async Task<string> NextOrderNumberAsync(int year, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"DealNumberCounters\" (\"Year\", \"LastValue\") VALUES ({year}, 0) ON CONFLICT (\"Year\") DO NOTHING",
            cancellationToken);
        var counter = await database.DealNumberCounters.FromSqlInterpolated(
            $"SELECT * FROM \"DealNumberCounters\" WHERE \"Year\" = {year} FOR UPDATE").SingleAsync(cancellationToken);
        return counter.Next();
    }

    // Saved inside the open transaction before the sale changes, because its guard judges the sale as it stands;
    // the refund and the cancellation still commit or roll back together.
    public async Task<Guid?> ReleaseReservationAsync(ReservationReleaseKind kind, Guid actorId, string reason,
        CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == Sale.Id, cancellationToken);
        var release = new ReservationRelease(Guid.NewGuid(), reservation, Sale.DueAt, kind, actorId, reason,
            context.Now);
        database.ReservationReleases.Add(release);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return release.Id;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        })
        {
            return null;
        }
    }

    public async Task<SaleStatus> CommitAsync(SalesOrderEvent entry, CancellationToken cancellationToken)
    {
        database.SalesOrderEvents.Add(entry);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SaleStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        })
        {
            // One open sale per trade-in, unique numbers and the database guards decide races the locks cannot.
            return SaleStatus.Conflict;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }
}
