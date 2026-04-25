using Dealership.Application.Accounts;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentReservationScenario(DevelopmentInspectionScenario Listing, Guid RegistrationKey,
    int Index, bool KeepActive)
{
    public Guid Id => Listing.OperationId($"reservation-{Index}");

    public Guid ReleaseId => Listing.OperationId($"reservation-release-{Index}");

    public Guid PublicationId => Listing.OperationId("listing-publication");

    public Guid? PromoId => Index % 3 == 0 ? DevelopmentPurchaseFixtures.Promos[0].Id : null;

    public string CancellationRole => (Index % 3) switch
    {
        0 => AccountRoles.Customer,
        1 => AccountRoles.BranchManager,
        _ => AccountRoles.GeneralManager
    };

    public string? Reason => CancellationRole == AccountRoles.Customer ? null :
        "Customer requested cancellation after reviewing the purchase terms.";

    public Guid CancellationActor(Guid customerId) => CancellationRole == AccountRoles.Customer ? customerId :
        DevelopmentFixtureManifest.Staff.Single(item => item.Role == CancellationRole &&
            (item.BranchId == null || item.BranchId == Listing.Intake.BranchId)).Id;
}

public static class DevelopmentReservationScenarios
{
    public const int ReleasedPerBranch = 51;

    public static IReadOnlyList<DevelopmentReservationScenario> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany((branch, branchIndex) =>
        {
            var listings = DevelopmentInspectionScenarios.All.Where(item =>
                item.Intake.BranchId == branch.Id && item.PublishListing).ToArray();
            var registrationKey = DevelopmentFixtureManifest.Customers[branchIndex + 1].Registration.RegistrationKey;
            var sellerReservations = Enumerable.Range(0, ReleasedPerBranch + 1).Select(index =>
                new DevelopmentReservationScenario(index == ReleasedPerBranch ? listings[^1] : listings[index % (listings.Length - 1)],
                    registrationKey, index, index == ReleasedPerBranch));
            // Leave the primary demo buyer free to reserve while retaining two full history pages and a partial page.
            var buyerReservations = Enumerable.Range(0, ReleasedPerBranch / 3).Select(index =>
                new DevelopmentReservationScenario(listings[index % (listings.Length - 1)],
                    DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey, ReleasedPerBranch + 1 + index, false));
            return sellerReservations.Concat(buyerReservations);
        }).ToArray());
}
