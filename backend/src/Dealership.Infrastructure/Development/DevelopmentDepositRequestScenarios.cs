namespace Dealership.Infrastructure.Development;

public enum DevelopmentDepositRequestOutcome
{
    Pending,
    Cancelled,
    Confirmed
}

public sealed record DevelopmentDepositRequestScenario(string Name, DevelopmentInspectionScenario Listing,
    DevelopmentTestDriveVolumeScenario Customer, TimeSpan Age, DevelopmentDepositRequestOutcome Outcome)
{
    public Guid Id => Listing.OperationId($"deposit-request-{Name}");

    public Guid PublicationId => Listing.OperationId("listing-publication");

    public Guid CustomerKey => Customer.Customer.Registration.RegistrationKey;

    public Guid StaffId => DevelopmentFixtureManifest.AgentFor(Listing.Intake.BranchId, Id).Id;
}

public static class DevelopmentDepositRequestScenarios
{
    // A request holds the car for 24 hours: the timer turns amber below twelve hours left and red below three.
    public static IReadOnlyList<DevelopmentDepositRequestScenario> All { get; } = Array.AsReadOnly(
    [
        Create("half-elapsed", 3, 25, TimeSpan.FromHours(13), DevelopmentDepositRequestOutcome.Pending),
        Create("near-expiry", 7, 24, TimeSpan.FromMinutes(22 * 60 + 30), DevelopmentDepositRequestOutcome.Pending),
        Create("cancelled", 9, 23, TimeSpan.FromHours(9), DevelopmentDepositRequestOutcome.Cancelled),
        Create("confirmed", 11, 22, TimeSpan.FromHours(4), DevelopmentDepositRequestOutcome.Confirmed)
    ]);

    // Requests use the first branch's published listings that are still on sale and carry no test drive, hold or
    // sale of their own (a hold or reservation would cancel the bookings), and buyers from the end of its unclaimed
    // test-drive queue, who hold no reservation of their own.
    private static DevelopmentDepositRequestScenario Create(string name, int listing, int customer, TimeSpan age,
        DevelopmentDepositRequestOutcome outcome)
    {
        var branchId = DevelopmentFixtureManifest.Branches[0].Id;
        var buyer = DevelopmentTestDriveVolumeManifest.All.Single(item => item.BranchId == branchId &&
            item.Queue == "ToClaim" && item.Index == customer);
        var vehicle = DevelopmentInspectionScenarios.All.Where(item => item.PublishListing &&
            item.Intake.BranchId == branchId).ElementAt(listing);
        return new(name, vehicle, buyer, age, outcome);
    }
}
