using System.Globalization;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

/// <summary>
/// Migrates one database per test run so every fixture clones it instead of replaying all migrations.
/// </summary>
internal static class TemplateDatabase
{
    private const string Prefix = "aa02_template_";
    private const string TimestampFormat = "yyyyMMddHHmmss";
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(6);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _name;
    private static string _maintenanceConnection = string.Empty;

    public static async Task<string> EnsureAsync(string maintenanceConnection)
    {
        await Gate.WaitAsync();

        try
        {
            if (_name is not null)
            {
                return _name;
            }

            _maintenanceConnection = maintenanceConnection;
            await DropStaleAsync();
            var name = $"{Prefix}{DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture)}_{Guid.NewGuid():N}";
            await CreateAsync(name);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Drop(name);
            _name = name;
            return name;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task CreateAsync(string name)
    {
        await using (var maintenance = new NpgsqlConnection(_maintenanceConnection))
        {
            await maintenance.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", maintenance);
            await create.ExecuteNonQueryAsync();
        }

        var settings = new NpgsqlConnectionStringBuilder(_maintenanceConnection) { Database = name };
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(settings.ConnectionString).Options;

        try
        {
            await using var database = new DealershipDbContext(options);
            await database.Database.MigrateAsync();
        }
        catch
        {
            Drop(name);
            throw;
        }

        // A template cannot be cloned while any session is connected to it.
        NpgsqlConnection.ClearAllPools();
    }

    private static async Task DropStaleAsync()
    {
        await using var maintenance = new NpgsqlConnection(_maintenanceConnection);
        await maintenance.OpenAsync();
        var names = new List<string>();
        await using (var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE 'aa02\\_template\\_%'", maintenance))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        foreach (var name in names.Where(IsStale))
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", maintenance);
            await drop.ExecuteNonQueryAsync();
        }
    }

    // Only templates from interrupted runs are removed; a recent name may belong to a concurrent run.
    private static bool IsStale(string name)
    {
        var stamp = name.Length >= Prefix.Length + TimestampFormat.Length
            ? name.Substring(Prefix.Length, TimestampFormat.Length)
            : string.Empty;

        return DateTime.TryParseExact(stamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal, out var createdAt) &&
            DateTime.UtcNow - createdAt > StaleAfter;
    }

    private static void Drop(string name)
    {
        // Names are owned, generated identities; never accept an external deletion target.
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Disposable template identity is invalid.");
        }

        using var maintenance = new NpgsqlConnection(_maintenanceConnection);
        maintenance.Open();
        using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", maintenance);
        drop.ExecuteNonQuery();
    }
}
