using Dealership.Application.Accounts;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentTestDriveVolumeScenario(Guid BranchId, string Queue, int Index,
    DevelopmentCustomerFixture Customer)
{
    public Guid HandlerId => DevelopmentFixtureManifest.AgentFor(BranchId, OperationId("claim")).Id;

    public Guid OperationId(string action) => DevelopmentInspectionScenarios.OperationId(BranchId,
        $"test-drive-volume/{Queue}/{Index}/{action}");
}

public static class DevelopmentTestDriveVolumeManifest
{
    // Just over the 25-row page size, so every paged queue shows a second page without repeated filler.
    public const int RowsPerQueue = 27;

    public static IReadOnlyList<DevelopmentTestDriveVolumeScenario> All { get; } = Array.AsReadOnly(
        DevelopmentFixtureManifest.Branches.SelectMany((branch, branchIndex) =>
            new[] { "History", "MyWork", "ToClaim" }.SelectMany(queue =>
                Enumerable.Range(0, RowsPerQueue).Select(index => new DevelopmentTestDriveVolumeScenario(
                    branch.Id, queue, index, Customer(branchIndex, queue, index))))).ToArray());

    private static DevelopmentCustomerFixture Customer(int branchIndex, string queue, int index)
    {
        var customerIndex = queue == "ToClaim" ? index + RowsPerQueue : index;
        if (queue == "History")
        {
            customerIndex = 0;
        }

        var identity = branchIndex * RowsPerQueue * 2 + customerIndex;
        string[] given = ["Aisyah", "Farhana", "Nadia", "Syafiqah", "Hidayah", "Amirah", "Izzati", "Sabrina",
            "Faizal", "Hakim", "Irfan", "Hafiz", "Azim", "Firdaus", "Ridzuan", "Syafiq", "Aiman", "Faris"];
        string[] family = ["Rahman", "Hassan", "Abdullah", "Ismail", "Ibrahim", "Yusof", "Osman", "Aziz",
            "Hamid", "Salleh", "Zainal", "Mahmud", "Omar", "Khalid", "Razak", "Idris", "Jamal"];
        var first = given[identity % given.Length];
        var last = family[identity / given.Length];
        string[] locations = ["shahalam", "petalingjaya", "bayanlepas"];
        var id = DevelopmentInspectionScenarios.OperationId(DevelopmentFixtureManifest.Branches[branchIndex].Id,
            $"test-drive-volume/customer/{customerIndex}");
        return new(new RegistrationRequest(id, $"{first} {last}",
            $"{first}.{last}.{locations[branchIndex]}@example.invalid".ToLowerInvariant(),
            DevelopmentFixtureManifest.Password, $"95061514{identity + 1000:0000}", $"012{identity + 3456000:0000000}",
            $"{customerIndex + 1} Jalan Melati, {DevelopmentFixtureManifest.Branches[branchIndex].City}",
            $"{first}.{last}.{locations[branchIndex]}".ToLowerInvariant()), true);
    }
}
