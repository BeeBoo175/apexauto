using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

// Owner-role and refusal statements shared by the retention tests.
internal static class RetentionSql
{
    // Runs one update with the table's guard trigger off; the trigger is enabled again even when the update fails.
    internal static async Task AgeTableAsync(NpgsqlConnection owner, string table, string assignments,
        string trigger = "USER")
    {
        await ExecuteAsync(owner, $"ALTER TABLE \"{table}\" DISABLE TRIGGER {trigger}");
        try
        {
            await ExecuteAsync(owner, $"UPDATE \"{table}\" SET {assignments}");
        }
        finally
        {
            await ExecuteAsync(owner, $"ALTER TABLE \"{table}\" ENABLE TRIGGER {trigger}");
        }
    }

    internal static async Task<string> FailureAsync(DealershipDbContext database, string sql, params object[] values)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(sql, values));
        var postgres = exception as PostgresException ?? exception.InnerException as PostgresException;
        return Assert.IsType<PostgresException>(postgres).SqlState;
    }

    internal static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Expects the statement to be refused with the SQL state; the surrounding transaction is rolled back.
    internal static async Task ExpectFailureAsync(DealershipDbContext database, string sqlState, string sql,
        params object[] arguments)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => database.Database.ExecuteSqlRawAsync(sql, arguments));
        var postgres = Assert.IsType<PostgresException>(failure is DbUpdateException ? failure.InnerException : failure);
        Assert.Equal(sqlState, postgres.SqlState);
        await transaction.RollbackAsync();
    }

    // The SQL state of the refusal, or null when the database accepted the statement.
    internal static async Task<string?> RuntimeFailureAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException failure)
        {
            return failure.SqlState;
        }
    }
}
