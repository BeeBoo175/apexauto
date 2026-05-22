using System.Security.Cryptography;
using System.Text;
using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentIntakeFixture(Guid Id, Guid BranchId, Guid RegistrationKey,
    string RegistrationPlate, ValuationRequest Vehicle, StaffWorklistStatus Status, CheckInEvidence? CheckIn);

public static class DevelopmentIntakeManifest
{
    public const int AppointmentsPerBranch = 12;
    public const int RepairRequestsPerBranch = 3;
    public const int InspectionExamplesPerBranch = 14;
    public const int CatalogueExamplesPerBranch = 14;
    public const int BaselineTotalPerBranch = AppointmentsPerBranch + RepairRequestsPerBranch + 4;
    public const int InspectionCheckedInPerBranch = RepairRequestsPerBranch + 2 + InspectionExamplesPerBranch;
    public const int CheckedInPerBranch = InspectionCheckedInPerBranch + CatalogueExamplesPerBranch;
    public const int TotalPerBranch = AppointmentsPerBranch + CheckedInPerBranch + 2;

    public static IReadOnlyList<DevelopmentIntakeFixture> Appointments { get; } =
        Array.AsReadOnly(Enumerable.Range(0, DevelopmentFixtureManifest.Branches.Count)
            .SelectMany(CreateBranch).ToArray());

    public static IReadOnlyList<DevelopmentIntakeFixture> InspectionExamples { get; } = Array.AsReadOnly(
        Appointments.GroupBy(item => item.BranchId).SelectMany(branch => branch.Skip(BaselineTotalPerBranch)
            .Take(InspectionExamplesPerBranch)).ToArray());

    public static IReadOnlyList<DevelopmentIntakeFixture> CatalogueExamples { get; } = Array.AsReadOnly(
        Appointments.GroupBy(item => item.BranchId).SelectMany(branch => branch
            .Skip(BaselineTotalPerBranch + InspectionExamplesPerBranch)).ToArray());

    public static Guid PhotoId(Guid appointmentId, PhotoAngle angle)
    {
        return Identity($"photo/{DevelopmentFixturePhotos.MediaVersion}/{appointmentId:N}/{angle}");
    }

    private static IEnumerable<DevelopmentIntakeFixture> CreateBranch(int branchIndex)
    {
        var branch = DevelopmentFixtureManifest.Branches[branchIndex];
        string[] prefixes = ["VCA", "BPL", "PQA"];
        return Enumerable.Range(0, TotalPerBranch).Select(index =>
        {
            var identityIndex = IdentityIndex(index);
            var customerIndex = (identityIndex + branchIndex) % 3 + 1;
            var registrationKey = DevelopmentFixtureManifest.Customers[customerIndex].Registration.RegistrationKey;
            var plate = $"{prefixes[branchIndex]} {6200 + identityIndex}";
            var catalogueIndex = index - BaselineTotalPerBranch - InspectionExamplesPerBranch;
            var vehicle = catalogueIndex >= 0
                ? CatalogueVehicle(catalogueIndex, branchIndex)
                : new ValuationRequest("Perodua", "Myvi", "1.5 AV", 2019, 45000 + identityIndex * 1200);
            var status = index switch
            {
                AppointmentsPerBranch => StaffWorklistStatus.Cancelled,
                AppointmentsPerBranch + 1 => StaffWorklistStatus.CheckedIn,
                AppointmentsPerBranch + 2 => StaffWorklistStatus.NoShow,
                < AppointmentsPerBranch => StaffWorklistStatus.Booked,
                _ => StaffWorklistStatus.CheckedIn
            };
            var mileage = catalogueIndex >= 0 ? vehicle.MileageKilometres!.Value : 96000;
            var evidence = status == StaffWorklistStatus.CheckedIn
                ? new CheckInEvidence($"PM2M800{branchIndex + 1}0KC00{6200 + identityIndex}", mileage, true, true, true, true,
                    "Customer attended with vehicle and ownership documents.")
                : null;
            return new DevelopmentIntakeFixture(Identity($"appointment/{branch.Id:N}/{identityIndex}"), branch.Id,
                registrationKey, plate, vehicle, status, evidence);
        });
    }

    private static int IdentityIndex(int index)
    {
        // Retain historical receipt, vehicle and sale identities independently of the smaller sample counts.
        if (index < AppointmentsPerBranch)
        {
            return index;
        }

        if (index < BaselineTotalPerBranch - 1)
        {
            return 41 + index - AppointmentsPerBranch;
        }

        return 95 + index - (BaselineTotalPerBranch - 1);
    }

    private static ValuationRequest CatalogueVehicle(int index, int branchIndex)
    {
        var vehicle = CatalogueVehicle(index);
        return vehicle with { MileageKilometres = vehicle.MileageKilometres + branchIndex * 3700 };
    }

    private static ValuationRequest CatalogueVehicle(int index) => index switch
    {
        0 or 1 => new("Perodua", "Ativa", "H", 2021, 42000),
        2 => new("Proton", "Saga", "Standard", 2019, 78000),
        3 => new("Proton", "Saga", "Standard", 2020, 65000),
        4 => new("Honda", "City", "S", 2022, 46000),
        5 => new("Honda", "City", "S", 2023, 28500),
        6 => new("Toyota", "Vios", "1.5 G", 2019, 82000),
        7 => new("Toyota", "Vios", "1.5 G", 2020, 63500),
        8 => new("Mazda", "Mazda 3", "2.0 High Plus", 2020, 58000),
        9 => new("Mazda", "Mazda 3", "2.0 High Plus", 2021, 41500),
        10 => new("BYD", "Dolphin", "Dynamic Standard", 2023, 32500),
        11 => new("BYD", "Dolphin", "Premium Extended", 2024, 18000),
        12 => new("Perodua", "Alza", "AV", 2022, 47500),
        13 => new("Honda", "City", "S", 2022, 46000),
        _ => throw new InvalidOperationException("The configured catalogue example has no vehicle specification.")
    };

    private static Guid Identity(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("apexauto/development/intake/" + name));
        // RFC 9562 version 8 reserves custom deterministic UUIDs without sentinel identities.
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return Guid.ParseExact(Convert.ToHexString(bytes.AsSpan(0, 16)), "N");
    }
}
