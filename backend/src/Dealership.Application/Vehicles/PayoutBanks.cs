namespace Dealership.Application.Vehicles;

// The SWIFT codes the provider accepts for a payout, as listed in its documentation, plus the sandbox's dummy bank
// that always succeeds.
public static class PayoutBanks
{
    public const string SandboxBankCode = "DUMMYBANKVERIFIED";

    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        ("PHBMMYKL", "Affin Bank"),
        ("AGOBMYKL", "Agrobank"),
        ("MFBBMYKL", "Alliance Bank"),
        ("RJHIMYKL", "Al Rajhi Bank"),
        ("ARBKMYKL", "AmBank"),
        ("BIMBMYKL", "Bank Islam"),
        ("BKRMMYKL", "Bank Rakyat"),
        ("BMMBMYKL", "Bank Muamalat"),
        ("BSNAMYK1", "BSN"),
        ("CIBBMYKL", "CIMB Bank"),
        ("CITIMYKL", "Citibank"),
        ("HLBBMYKL", "Hong Leong Bank"),
        ("HBMBMYKL", "HSBC"),
        ("KFHOMYKL", "Kuwait Finance House"),
        ("MBBEMYKL", "Maybank"),
        ("OCBCMYKL", "OCBC Bank"),
        ("PBBEMYKL", "Public Bank"),
        ("RHBBMYKL", "RHB Bank"),
        ("SCBLMYKX", "Standard Chartered"),
        ("UOVBMYKL", "UOB")
    ];

    public static bool IsSupported(string code) =>
        code == SandboxBankCode || All.Any(bank => bank.Code == code);
}
