using Dealership.Domain.Sales;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentSaleVolumeManifest
{
    // The buyers must exist before the snapshot dates their holds, so the intake unit registers them first.
    public static IReadOnlyList<DevelopmentCustomerFixture> Customers => [.. All.Select(sale =>
        DevelopmentTestDriveVolumeManifest.All.First(item => item.Queue == "MyWork" &&
            item.Customer.Registration.RegistrationKey == sale.Hold.RegistrationKey).Customer)];

    public static IReadOnlyList<DevelopmentSaleScenario> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany(branch =>
        {
            var customers = DevelopmentTestDriveVolumeManifest.All.Where(item =>
                item.BranchId == branch.Id && item.Queue == "MyWork").OrderBy(item => item.Index).ToArray();
            return DevelopmentInspectionScenarios.All.Where(item =>
                item.Intake.BranchId == branch.Id && DevelopmentIntakeManifest.SalesExamples.Contains(item.Intake))
                .Select((listing, index) => new DevelopmentSaleScenario(
                    new(listing, customers[index].Customer.Registration.RegistrationKey, 0, true),
                    SalesOrderStatus.Handling, false, 300m));
        }).ToArray());
}
