using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

[Trait("Category", "SeedPipeline")]
public sealed class DevelopmentFixtureTargetTests
{
    [Theory]
    [InlineData("Production", "apexauto_local", "localhost")]
    [InlineData("Development", "another_database", "localhost")]
    [InlineData("Development", "apexauto_local", "database.example.invalid")]
    [InlineData("Development", "apexauto_local", "localhost,remote.example.invalid")]
    public void FixturesRefuseOtherEnvironmentsTargetsAndRemoteHosts(string environment, string database,
        string host)
    {
        var connection = $"Host={host};Database=apexauto_local;Username=local;Password=local";
        Assert.Throws<InvalidOperationException>(() => DevelopmentFixtureTarget.Validate(environment, database, connection));
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("template1")]
    public void FixturesCannotTargetMaintenanceOrTemplateDatabases(string database)
    {
        Assert.Throws<InvalidOperationException>(() => DevelopmentFixtureTarget.Validate("Development", database,
            $"Host=localhost;Database={database};Username=local;Password=local"));
    }

    [Theory]
    [InlineData("Host", "127.0.0.1")]
    [InlineData("Port", "5433")]
    [InlineData("Database", "another_database")]
    [InlineData("Username", "another_user")]
    [InlineData("Password", "another_password")]
    public void TargetMustMatchEveryServiceConnectionIdentityField(string field, string differentValue)
    {
        const string connection = "Host=localhost;Port=5432;Database=apexauto_local;Username=local;Password=local";
        var target = DevelopmentFixtureTarget.Validate("Development", "apexauto_local", connection);
        target.EnsureMatches(connection);
        var different = new Npgsql.NpgsqlConnectionStringBuilder(connection)
        {
            [field] = differentValue
        };

        Assert.Throws<InvalidOperationException>(() => target.EnsureMatches(different.ConnectionString));
    }
}
