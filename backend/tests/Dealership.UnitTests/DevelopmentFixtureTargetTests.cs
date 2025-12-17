using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

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
}
