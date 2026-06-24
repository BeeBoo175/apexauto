using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Dealership.Infrastructure;
using Dealership.Application;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.IntegrationTests;

public sealed class RegistrationDatabase : IAsyncLifetime
{
    private readonly string _databaseName = $"aa02_registration_{Guid.NewGuid():N}";
    private readonly string _runtimeRole = $"aa02_runtime_{Guid.NewGuid():N}";
    private string _maintenanceConnection = string.Empty;
    private bool _databaseCreated;
    private bool _roleCreated;

    public string ConnectionString { get; private set; } = string.Empty;

    public string RuntimeConnectionString { get; private set; } = string.Empty;

    public ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = RuntimeConnectionString }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public async Task InitializeAsync()
    {
        DotEnvLoader.LoadForDevelopment(Directory.GetCurrentDirectory());
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Local PostgreSQL configuration is required for registration integrity tests.");
        }

        var settings = new NpgsqlConnectionStringBuilder(configured);
        var configuredDatabase = Environment.GetEnvironmentVariable("POSTGRES_DB");

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development" ||
            string.IsNullOrWhiteSpace(configuredDatabase) || settings.Database != configuredDatabase ||
            settings.Host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException("Registration tests require Development and the explicitly configured local PostgreSQL target.");
        }

        settings.IncludeErrorDetail = false;
        settings.Pooling = false;
        _maintenanceConnection = settings.ConnectionString;

        try
        {
            await ProvisionAsync(settings);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private async Task ProvisionAsync(NpgsqlConnectionStringBuilder settings)
    {
        var template = await TemplateDatabase.EnsureAsync(_maintenanceConnection);
        await using var maintenance = new NpgsqlConnection(_maintenanceConnection);
        await maintenance.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\" TEMPLATE \"{template}\"", maintenance))
        {
            await create.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        settings.Database = _databaseName;
        ConnectionString = settings.ConnectionString;

        var password = Guid.NewGuid().ToString("N");
        await using (var createRole = new NpgsqlCommand($"CREATE ROLE \"{_runtimeRole}\" LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE", maintenance))
        {
            await createRole.ExecuteNonQueryAsync();
            _roleCreated = true;
        }

        await using var target = new NpgsqlConnection(ConnectionString);
        await target.OpenAsync();
        await using (var grants = new NpgsqlCommand($"GRANT CONNECT ON DATABASE \"{_databaseName}\" TO \"{_runtimeRole}\"; GRANT USAGE ON SCHEMA public TO \"{_runtimeRole}\"; GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO \"{_runtimeRole}\"", target))
        {
            await grants.ExecuteNonQueryAsync();
        }

        settings.Username = _runtimeRole;
        settings.Password = password;
        RuntimeConnectionString = settings.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (!_databaseCreated && !_roleCreated)
        {
            return;
        }

        // Names are owned, random fixture identities; never accept an external deletion target.
        if (!_databaseName.StartsWith("aa02_registration_", StringComparison.Ordinal) ||
            !_runtimeRole.StartsWith("aa02_runtime_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Disposable fixture identity is invalid.");
        }

        await using var maintenance = new NpgsqlConnection(_maintenanceConnection);
        await maintenance.OpenAsync();

        if (_databaseCreated)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", maintenance);
            await drop.ExecuteNonQueryAsync();
        }

        if (_roleCreated)
        {
            await using var drop = new NpgsqlCommand($"DROP ROLE \"{_runtimeRole}\"", maintenance);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
