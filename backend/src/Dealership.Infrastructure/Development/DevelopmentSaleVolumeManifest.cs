using Dealership.Domain.Sales;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentSaleVolumeManifest
{
    // The buyers must exist before the snapshot dates their holds, so the intake unit registers them first.
    public static IReadOnlyList<DevelopmentCustomerFixture> Customers => [.. All.Select(sale =>
        DevelopmentTestDriveVolumeManifest.All.First(item => item.Queue != "History" &&
            item.Customer.Registration.RegistrationKey == sale.Hold.RegistrationKey).Customer)];

    public static IReadOnlyList<DevelopmentSaleScenario> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany(branch =>
        {
            // Handling sales take the MyWork buyers; the older completed purchases take the first ToClaim buyers.
            var customers = DevelopmentTestDriveVolumeManifest.All.Where(item =>
                item.BranchId == branch.Id && item.Queue == "MyWork").OrderBy(item => item.Index)
                .Concat(DevelopmentTestDriveVolumeManifest.All.Where(item =>
                    item.BranchId == branch.Id && item.Queue == "ToClaim").OrderBy(item => item.Index))
                .ToArray();
            return DevelopmentInspectionScenarios.All.Where(item =>
                item.Intake.BranchId == branch.Id && DevelopmentIntakeManifest.SalesExamples.Contains(item.Intake))
                .Select((listing, index) => new DevelopmentSaleScenario(
                    new(listing, customers[index].Customer.Registration.RegistrationKey, 0, true),
                    index < DevelopmentTestDriveVolumeManifest.RowsPerQueue
                        ? SalesOrderStatus.Handling
                        : SalesOrderStatus.Delivered,
                    false, 300m,
                    HeldStep: index < DevelopmentTestDriveVolumeManifest.RowsPerQueue
                        ? 5
                        : index - DevelopmentTestDriveVolumeManifest.RowsPerQueue + 2));
        }).ToArray());
}
