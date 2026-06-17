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
    public const int ReleasedPerBranch = 12;
    private const int ActiveHoldIndex = 51;
    public const int FirstBuyerIndex = 52;

    public static IReadOnlyList<DevelopmentReservationScenario> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany((branch, branchIndex) =>
        {
            var listings = DevelopmentInspectionScenarios.All.Where(item =>
                item.Intake.BranchId == branch.Id && item.PublishListing &&
                !DevelopmentIntakeManifest.SalesExamples.Contains(item.Intake)).ToArray();
            var registrationKey = DevelopmentFixtureManifest.Customers[branchIndex + 1].Registration.RegistrationKey;
            var sellerReservations = Enumerable.Range(0, ReleasedPerBranch + 1).Select(index =>
                new DevelopmentReservationScenario(index == ReleasedPerBranch ? listings[^1] : listings[index % (listings.Length - 1)],
                    registrationKey, index == ReleasedPerBranch ? ActiveHoldIndex : index, index == ReleasedPerBranch));
            // Keep cancellation roles and promo variations visible without repeating whole worklists.
            // Retained receipt identities do not depend on the sample count.
            var buyerReservations = Enumerable.Range(0, ReleasedPerBranch / 3).Select(index =>
                new DevelopmentReservationScenario(listings[index % (listings.Length - 1)],
                    DevelopmentFixtureManifest.Customers[0].Registration.RegistrationKey, FirstBuyerIndex + index, false));
            return sellerReservations.Concat(buyerReservations);
        }).Concat(SaleHolds()).ToArray());

    private static IEnumerable<DevelopmentReservationScenario> SaleHolds()
    {
        static DevelopmentReservationScenario Hold(int branch, int listing, int customer, int index) => new(
            DevelopmentInspectionScenarios.PublishedAt(branch, listing),
            DevelopmentFixtureManifest.Customers[customer].Registration.RegistrationKey, index, true);

        // Completed purchases release the customer's active slot before the next purchase is confirmed.
        // Catalogue choices retain the distinct inspection examples, all makes and equal-price listings.
        yield return Hold(0, 6, 0, 70);
        yield return Hold(1, 6, 0, 71);
        yield return Hold(2, 6, 2, 72);
        yield return Hold(0, 8, 4, 73);
        yield return Hold(1, 8, 5, 74);
        // One approved sale carries a warranty and one returns to handling after its discount is rejected.
        yield return Hold(0, 10, 6, 75);
        yield return Hold(1, 10, 7, 76);
    }
}
