using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dealership.Infrastructure.Development.Maintenance;

internal sealed class DevelopmentSnapshotInitialization : IAsyncDisposable
{
    private readonly DealershipDbContext _database;
    private readonly IDbContextTransaction _transaction;
    private readonly List<string> _liveDefinitions = [];

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
                    public."AppointmentCancellations", public."AppointmentCheckIns", public."Vehicles",
                    public."AppointmentMailOutbox", public."PhotoRemovals" IN ACCESS EXCLUSIVE MODE;
                """, cancellationToken);
            if (await database.IntakeAppointments.AnyAsync(cancellationToken) ||
                await database.IntakePhotos.AnyAsync(cancellationToken))
            {
                throw new InvalidOperationException("Snapshot initialization requires empty intake tables. Deliberately clear then seed.");
            }

            // Transactional DDL is private until commit; restore every definition before releasing the table locks.
            await initialization.UseEventTimeAsync("require_available_appointment_photo",
                "(SELECT a.\"CreatedAt\" FROM public.\"IntakeAppointments\" a WHERE a.\"Id\" = NEW.\"AppointmentId\")", 2, cancellationToken);
            await initialization.UseEventTimeAsync("validate_appointment_cancellation", "NEW.\"CancelledAt\"", 1, cancellationToken);
            await initialization.UseEventTimeAsync("validate_appointment_checkin", "NEW.\"CheckedInAt\"", 3, cancellationToken);
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
