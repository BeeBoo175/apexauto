using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Development.Maintenance;

internal sealed class DevelopmentSnapshotInitialization : IAsyncDisposable
{
    private readonly DealershipDbContext _database;
    private readonly IDbContextTransaction _transaction;
    private readonly List<string> _liveDefinitions = [];

    private sealed record EventTimeGuard(string Function, string EventTime, int ClockReferences);

    // Each guard keeps every ownership, eligibility and state check; only its clock read becomes the recorded event time.
    private static readonly EventTimeGuard[] EventTimeGuards =
    [
        new("require_available_appointment_photo",
            "(SELECT a.\"CreatedAt\" FROM public.\"IntakeAppointments\" a WHERE a.\"Id\" = NEW.\"AppointmentId\")", 2),
        new("validate_appointment_cancellation", "NEW.\"CancelledAt\"", 1),
        new("validate_appointment_checkin", "NEW.\"CheckedInAt\"", 3),
        new("guard_appointment_assignment", "NEW.\"RecordedAt\"", 2),
        new("guard_inspection_signed_revision", "NEW.\"SignedAt\"", 1),
        new("guard_inspection_repair_request", "NEW.\"RequestedAt\"", 1),
        new("guard_inspection_repair_approval", "NEW.\"ApprovedAt\"", 1),
        new("guard_inspection_repair_completion", "NEW.\"CompletedAt\"", 1),
        new("guard_listing_operation", "NEW.\"RecordedAt\"", 1),
        new("guard_listing_photo", "GREATEST(NEW.\"CreatedAt\", NEW.\"AvailableAt\")", 2),
        new("guard_reservation", "NEW.\"CreatedAt\"", 3),
        new("guard_reservation_release", "NEW.\"RecordedAt\"", 2),
        new("guard_sales_order_event", "NEW.\"RecordedAt\"", 1),
        new("guard_sale_settlement", "NEW.\"SettledAt\"", 1),
        new("guard_sale_handover", "NEW.\"DeliveredAt\"", 1),
        new("guard_test_drive_booking", "NEW.\"CreatedAt\"", 3),
        new("guard_test_drive_assignment", "NEW.\"RecordedAt\"", 4),
        new("guard_test_drive_handling", "recorded_at", 3),
        new("guard_test_drive_cancellation", "NEW.\"RecordedAt\"", 11)
    ];

    private DevelopmentSnapshotInitialization(DealershipDbContext database, IDbContextTransaction transaction)
    {
        _database = database;
        _transaction = transaction;
    }

    public static async Task<DevelopmentSnapshotInitialization> BeginAsync(DealershipDbContext database,
        DevelopmentFixtureTarget target, CancellationToken cancellationToken)
    {
        target.EnsureMatches(database.Database.GetConnectionString());
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var initialization = new DevelopmentSnapshotInitialization(database, transaction);
        try
        {
            await database.Database.ExecuteSqlRawAsync("""
                LOCK TABLE public."IntakeAppointments", public."IntakePhotos", public."AppointmentPhotos",
                    public."AppointmentAssignments", public."AppointmentCheckIns", public."Vehicles",
                    public."AppointmentMailOutbox", public."PhotoRemovals" IN ACCESS EXCLUSIVE MODE;
                """, cancellationToken);
            if (await database.IntakeAppointments.AnyAsync(cancellationToken) ||
                await database.IntakePhotos.AnyAsync(cancellationToken))
            {
                throw new InvalidOperationException("Snapshot initialization requires empty intake tables. Deliberately clear then seed.");
            }

            // Transactional DDL is private until commit; restore every definition before releasing the table locks.
            foreach (var guard in EventTimeGuards)
            {
                await initialization.UseEventTimeAsync(guard.Function, guard.EventTime, guard.ClockReferences,
                    cancellationToken);
            }

            return initialization;
        }
        catch
        {
            await initialization.DisposeAsync();
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _database.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE", cancellationToken);
        foreach (var definition in _liveDefinitions)
        {
            await _database.Database.ExecuteSqlRawAsync(definition, cancellationToken);
        }

        await _transaction.CommitAsync(cancellationToken);
    }

    private async Task UseEventTimeAsync(string function, string eventTime, int expectedReferences,
        CancellationToken cancellationToken)
    {
        var definition = await _database.Database.SqlQuery<string>(
            $"SELECT pg_get_functiondef(to_regprocedure({"public." + function + "()"})) AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (definition.Split("clock_timestamp()", StringSplitOptions.None).Length - 1 != expectedReferences)
        {
            throw new InvalidOperationException("Snapshot initialization does not recognize the current admission guard. Review the changed schema.");
        }

        _liveDefinitions.Add(definition);
        await _database.Database.ExecuteSqlRawAsync(definition.Replace("clock_timestamp()", eventTime,
            StringComparison.Ordinal), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        _database.ChangeTracker.Clear();
    }
}
