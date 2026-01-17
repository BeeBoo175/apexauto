using System.Security.Cryptography;
using System.Text;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentIntakeFixture(Guid Id, Guid BranchId, Guid RegistrationKey,
    string RegistrationPlate, ValuationRequest Vehicle);

public static class DevelopmentIntakeManifest
{
    public const int AppointmentsPerBranch = 41;

    public static IReadOnlyList<DevelopmentIntakeFixture> Appointments { get; } =
        Array.AsReadOnly(Enumerable.Range(0, DevelopmentFixtureManifest.Branches.Count)
            .SelectMany(CreateBranch).ToArray());

    public static Guid PhotoId(Guid appointmentId, PhotoAngle angle)
    {
        return Identity($"photo/{appointmentId:N}/{angle}");
    }

    private static IEnumerable<DevelopmentIntakeFixture> CreateBranch(int branchIndex)
    {
        var branch = DevelopmentFixtureManifest.Branches[branchIndex];
        string[] prefixes = ["VCA", "BPL", "PQA"];
        return Enumerable.Range(0, AppointmentsPerBranch).Select(index =>
        {
            var customerIndex = (index + branchIndex) % 3 + 1;
            var registrationKey = DevelopmentFixtureManifest.Customers[customerIndex].Registration.RegistrationKey;
            var plate = $"{prefixes[branchIndex]} {6200 + index}";
            var vehicle = new ValuationRequest("Perodua", "Myvi", "1.5 AV", 2019, 45000 + index * 1200);
            return new DevelopmentIntakeFixture(Identity($"appointment/{branch.Id:N}/{index}"), branch.Id,
                registrationKey, plate, vehicle);
        });
    }

    private static Guid Identity(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("apexauto/development/intake/" + name));
        // RFC 9562 version 8 reserves custom deterministic UUIDs without sentinel identities.
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return Guid.ParseExact(Convert.ToHexString(bytes.AsSpan(0, 16)), "N");
    }
}
