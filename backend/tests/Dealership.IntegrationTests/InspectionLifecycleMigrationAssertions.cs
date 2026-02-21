using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dealership.IntegrationTests;

internal static class InspectionLifecycleMigrationAssertions
{
    public static async Task VerifyAsync(RegistrationDatabase fixture, IServiceProvider services)
    {
        var snapshot = await DevelopmentIntakeFixtureAssertions.SnapshotAsync(services);
        var options = new DbContextOptionsBuilder<DealershipDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        await using var database = new DealershipDbContext(options);
        var migrator = database.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync("20261022110807_InspectionRepairCompletions");
            Assert.All(await database.Vehicles.AsNoTracking().ToArrayAsync(), vehicle =>
                Assert.Equal(VehicleStatus.PendingInspection, vehicle.Status));
        }
        finally
        {
            await migrator.MigrateAsync("20261022113212_InspectionVehicleLifecycle");
        }
        Assert.Equal(snapshot, await DevelopmentIntakeFixtureAssertions.SnapshotAsync(services));
    }
}
