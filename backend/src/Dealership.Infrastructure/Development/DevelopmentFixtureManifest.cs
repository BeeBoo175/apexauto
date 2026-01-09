using Dealership.Application.Accounts;
using Dealership.Application.Branches;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentFixtureManifest
{
    public const string Password = "Password123!";

    public static IReadOnlyList<BranchProvisioningRequest> Branches { get; } = Array.AsReadOnly<BranchProvisioningRequest>(
    [
        new(Guid.Parse("c77708bf-1a31-42f5-9a69-0c8766697a52"), "ApexAuto Glenmarie",
            "12 Jalan Pengaturcara U1/51A", "Shah Alam", "Selangor", "+60355651234"),
        new(Guid.Parse("68f371ad-223a-43d6-b45b-b878eece79ae"), "ApexAuto Petaling Jaya",
            "22 Jalan SS 2/55", "Petaling Jaya", "Selangor", "+60378751234"),
        new(Guid.Parse("a50f99b1-8ff4-4ffb-b716-c82aa21fe117"), "ApexAuto Bayan Lepas",
            "8 Jalan Mahsuri", "Bayan Lepas", "Pulau Pinang", "+6046411234")
    ]);

    public static RegistrationRequest Customer { get; } = new(
        Guid.Parse("af478aac-d87c-44f8-b95f-180565b08ebc"), "Nur Aisyah Rahman",
        "nur.aisyah@example.invalid", Password, "940215140865", "0123456789",
        "18 Jalan Melati, Petaling Jaya, Selangor");

    public static IReadOnlyList<StaffProvisioningRequest> Staff { get; } = Array.AsReadOnly<StaffProvisioningRequest>(
    [
        new(Guid.Parse("d15eb7ab-de96-46fa-bd3c-057384244131"), "aina.sofia@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[0].Id),
        new(Guid.Parse("ac637058-c306-4ddf-a08b-aa724ca48e61"), "azlan.rahim@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[0].Id),
        new(Guid.Parse("6284a941-2479-4cdd-8794-2919d9a22ab2"), "siti.aminah@example.invalid",
            Password, AccountRoles.BranchManager, Branches[0].Id),
        new(Guid.Parse("eef92f15-3178-4573-92ba-27e6b6f675d0"), "farah.aziz@example.invalid",
            Password, AccountRoles.GeneralManager, null),
        new(Guid.Parse("e23b4856-dc3d-4e11-ad84-b5e409bb5167"), "muhammad.hafiz@example.invalid",
            Password, AccountRoles.Administrator, null),
        new(Guid.Parse("86ad2123-6c9b-49b6-ae90-12e23bac7bd3"), "daniel.wong@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[1].Id),
        new(Guid.Parse("d50e876c-867f-4c68-9854-6b7c3d017d31"), "arif.hakimi@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[1].Id),
        new(Guid.Parse("00ba453c-12c0-4b20-9401-c76c44926796"), "lim.mei.ling@example.invalid",
            Password, AccountRoles.BranchManager, Branches[1].Id),
        new(Guid.Parse("7e870e1a-d129-4c42-b1b7-5a8f90bd3707"), "ong.jia.hao@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[2].Id),
        new(Guid.Parse("2fbe1418-c72f-4fb6-9aa8-672795212a1c"), "mohd.firdaus@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[2].Id),
        new(Guid.Parse("12d6d08e-921a-45e7-8c20-f37e8ca8b0e3"), "nur.syafiqah@example.invalid",
            Password, AccountRoles.BranchManager, Branches[2].Id)
    ]);
}
