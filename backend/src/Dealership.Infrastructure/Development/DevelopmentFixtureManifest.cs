using Dealership.Application.Accounts;
using Dealership.Application.Branches;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentFixtureManifest
{
    public const string Password = "Password123!";

    public static BranchProvisioningRequest Branch { get; } = new(
        Guid.Parse("c77708bf-1a31-42f5-9a69-0c8766697a52"), "ApexAuto Glenmarie",
        "12 Jalan Pengaturcara U1/51A", "Shah Alam", "Selangor", "+60355651234");

    public static RegistrationRequest Customer { get; } = new(
        Guid.Parse("af478aac-d87c-44f8-b95f-180565b08ebc"), "Nur Aisyah Rahman",
        "nur.aisyah@example.invalid", Password, "940215140865", "0123456789",
        "18 Jalan Melati, Petaling Jaya, Selangor");

    public static IReadOnlyList<StaffProvisioningRequest> Staff { get; } = Array.AsReadOnly<StaffProvisioningRequest>(
    [
        new(Guid.Parse("d15eb7ab-de96-46fa-bd3c-057384244131"), "aina.sofia@example.invalid",
            Password, AccountRoles.SalesAgent, Branch.Id),
        new(Guid.Parse("ac637058-c306-4ddf-a08b-aa724ca48e61"), "azlan.rahim@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branch.Id),
        new(Guid.Parse("6284a941-2479-4cdd-8794-2919d9a22ab2"), "siti.aminah@example.invalid",
            Password, AccountRoles.BranchManager, Branch.Id),
        new(Guid.Parse("eef92f15-3178-4573-92ba-27e6b6f675d0"), "farah.aziz@example.invalid",
            Password, AccountRoles.GeneralManager, null),
        new(Guid.Parse("e23b4856-dc3d-4e11-ad84-b5e409bb5167"), "muhammad.hafiz@example.invalid",
            Password, AccountRoles.Administrator, null)
    ]);
}
