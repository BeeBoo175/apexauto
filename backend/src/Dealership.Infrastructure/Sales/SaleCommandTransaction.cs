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
        database.PurchaseSettings.FromSqlInterpolated($"SELECT * FROM \"PurchaseSettings\" WHERE \"Id\" = {PurchaseSettings.SingletonId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    // Only an active package the grade signed for this reservation's listing qualifies for can be sold.
    public async Task<SaleWarranty?> ResolveWarrantyAsync(Guid packageId, CancellationToken cancellationToken)
    {
        var package = await database.WarrantyPackages.FromSqlInterpolated(
            $"SELECT * FROM \"WarrantyPackages\" WHERE \"Id\" = {packageId} FOR SHARE").AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (package is null)
        {
            return null;
        }

        var grade = await SaleGrades.SignedGradeAsync(database, Sale.Id, cancellationToken);
        return package.IsEligibleFor(grade) ? new SaleWarranty(package.Id, package.Name, package.Price) : null;
    }

    public async Task<SaleTradeInChoice?> ResolveTradeInAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"IntakeAppointments\" WHERE \"Id\" = {appointmentId} FOR SHARE", cancellationToken);
        var appointment = await database.IntakeAppointments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
        var eligible = appointment is not null &&
            await SaleTradeIns.Eligible(database, Sale).AnyAsync(item => item.Id == appointmentId, cancellationToken);
        if (!eligible)
        {
            return null;
        }

        var taken = await SaleTradeIns.TakenByOtherSales(database, Sale.Id)
            .AnyAsync(item => item == appointmentId, cancellationToken);
        return new SaleTradeInChoice(appointment!.Id, SaleLabels.TradeIn(appointment), appointment.Valuation.Maximum,
            taken);
    }

    public Task<string> NextOrderNumberAsync(int year, CancellationToken cancellationToken) =>
        YearlyNumberAllocator.NextAsync<DealNumberCounter>(database, year, cancellationToken);

    public Task<bool> ReviewSettlementAsync(SaleSettlementReview review, CancellationToken cancellationToken) =>
        SaleSettlementDocuments.ReviewAsync(database, Sale, review, cancellationToken);

    public async Task<Guid?> LockTradeInVehicleAsync(CancellationToken cancellationToken)
    {
        if (Sale.TradeInAppointmentId is not { } appointmentId)
        {
            return null;
        }

        var vehicle = await database.Vehicles.FromSqlInterpolated(
            $"SELECT * FROM \"Vehicles\" WHERE \"IntakeAppointmentId\" = {appointmentId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (vehicle is null || vehicle.IntakeOwnerId != Sale.CustomerId || vehicle.Id == Sale.VehicleId)
        {
            return null;
        }

        var acquired = await database.SalesOrders.AnyAsync(item => item.AcquiredVehicleId == vehicle.Id,
            cancellationToken);
        return acquired ? null : vehicle.Id;
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

    public async Task<SaleResultStatus> CommitAsync(SalesOrderEvent entry, CancellationToken cancellationToken)
    {
        database.SalesOrderEvents.Add(entry);
        try
        {
            if (entry.Kind == SalesOrderEventKind.Settled)
            {
                await SaleSettlementDocuments.IssueAsync(database, Sale, entry, cancellationToken);
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SaleResultStatus.Succeeded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        })
        {
            // One open sale per trade-in, unique numbers and the database guards decide races the locks cannot.
            return SaleResultStatus.Conflict;
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation)
        {
            return SaleResultStatus.Conflict;
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
