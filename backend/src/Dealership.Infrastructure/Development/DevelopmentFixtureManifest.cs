using Dealership.Application.Accounts;
using Dealership.Application.Branches;

namespace Dealership.Infrastructure.Development;

public sealed record DevelopmentCustomerFixture(RegistrationRequest Registration, bool VerifyEmail);

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

    public static IReadOnlyList<DevelopmentCustomerFixture> Customers { get; } = Array.AsReadOnly<DevelopmentCustomerFixture>(
    [
        new(new RegistrationRequest(Guid.Parse("af478aac-d87c-44f8-b95f-180565b08ebc"), "Nur Aisyah Rahman",
            "nur.aisyah@example.invalid", Password, "940215140865", "0123456789",
            "18 Jalan Melati, Petaling Jaya, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("bb0a5d5d-54e8-4bc6-bca9-e18e0e6ecccf"), "Ahmad Faizal Hassan",
            "ahmad.faizal@example.invalid", Password, "890612105137", "0132456789",
            "24 Jalan Anggerik, Shah Alam, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("6863d91e-5c35-438d-b566-527d5c35de4b"), "Tan Hui Min",
            "tan.hui.min@example.invalid", Password, "920823145268", "0162345678",
            "16 Jalan SS 2/44, Petaling Jaya, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("304a1554-2604-46f6-b0ef-572801ca0902"), "Kavitha Rajan",
            "kavitha.rajan@example.invalid", Password, "910507075486", "0173456789",
            "32 Persiaran Mahsuri, Bayan Lepas, Pulau Pinang"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("60a30b41-f522-4b66-a4b9-9861032405d2"), "Muhammad Irfan Abdullah",
            "irfan.abdullah@example.invalid", Password, "900316105531", "0192846137",
            "9 Jalan Pelangi U5/3, Shah Alam, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("e735944a-9c3d-4482-853f-651be22c837a"), "Chong Wei Ling",
            "chong.wei.ling@example.invalid", Password, "930712145284", "0128473956",
            "41 Jalan SS 3/18, Petaling Jaya, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("3c753877-5cb5-4f5f-918f-392d0ac6c087"), "Lim Jia Wei",
            "lim.jia.wei@example.invalid", Password, "970904105238", "0112345876",
            "7 Jalan Cempaka 3/2, Shah Alam, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("a3cd0603-3b4a-420e-8384-1d6492d04805"), "Mohd Shafiq Zulkifli",
            "shafiq.zulkifli@example.invalid", Password, "880127075519", "0143219876",
            "15 Lorong Kenanga 2, Petaling Jaya, Selangor"), VerifyEmail: true),
        new(new RegistrationRequest(Guid.Parse("c702bbce-93c3-41e9-ad63-3380ae727ae4"), "Priya Nair",
            "priya.nair@example.invalid", Password, "010318140772", "0165432198",
            "23 Lebuh Pantai Jerejak, Bayan Lepas, Pulau Pinang"), VerifyEmail: true)
    ]);

    public static IReadOnlyList<StaffProvisioningRequest> ReliefTechnicians { get; } = Array.AsReadOnly<StaffProvisioningRequest>(
    [
        new(Guid.Parse("71afccdb-f74c-44b1-a122-c33ae580d92f"), "farid.hakim@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[0].Id),
        new(Guid.Parse("d7d08a9c-2fc8-4b2b-b102-7bc31a572814"), "lee.jun.wei@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[1].Id),
        new(Guid.Parse("96e5687b-51ec-484d-8b3d-871173210b2d"), "amirul.izzat@example.invalid",
            Password, AccountRoles.WorkshopTechnician, Branches[2].Id)
    ]);

    public static IReadOnlyList<StaffProvisioningRequest> AdditionalAgents { get; } = Array.AsReadOnly<StaffProvisioningRequest>(
    [
        new(Guid.Parse("c202854c-3fc6-4224-a159-3ba88a2eca18"), "nurul.huda@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[0].Id),
        new(Guid.Parse("2c51d2a2-3a7a-4d2f-90a1-5695ad6dd23f"), "kelvin.tan@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[1].Id),
        new(Guid.Parse("841698d7-2709-43f6-88c0-6395d62de946"), "suresh.kumar@example.invalid",
            Password, AccountRoles.SalesAgent, Branches[2].Id)
    ]);

    public static StaffProvisioningRequest PrimaryAgent(Guid branchId) => Staff.Single(item =>
        item.BranchId == branchId && item.Role == AccountRoles.SalesAgent &&
        !AdditionalAgents.Any(additional => additional.Id == item.Id));

    // Fixture identifiers are hash-derived, so one byte spreads work evenly and repeatably across a branch's staff.
    public static StaffProvisioningRequest AgentFor(Guid branchId, Guid key)
    {
        StaffProvisioningRequest[] agents =
        [
            PrimaryAgent(branchId),
            AdditionalAgents.Single(item => item.BranchId == branchId)
        ];
        return agents[key.ToByteArray()[0] % agents.Length];
    }

    public static StaffProvisioningRequest CheckInHandlerFor(Guid branchId, Guid key)
    {
        StaffProvisioningRequest[] handlers =
        [
            PrimaryAgent(branchId),
            AdditionalAgents.Single(item => item.BranchId == branchId),
            Staff.Single(item => item.BranchId == branchId && item.Role == AccountRoles.BranchManager)
        ];
        return handlers[key.ToByteArray()[1] % handlers.Length];
    }

    public static StaffProvisioningRequest PrimaryTechnician(Guid branchId) => Staff.Single(item =>
        item.BranchId == branchId && item.Role == AccountRoles.WorkshopTechnician &&
        !ReliefTechnicians.Any(relief => relief.Id == item.Id));

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
            Password, AccountRoles.BranchManager, Branches[2].Id),
        .. ReliefTechnicians,
        .. AdditionalAgents
    ]);
}
