namespace Dealership.Infrastructure.Development;

// The published sample accounts share one password shown on the sign-in page, so any visitor could erase them. They
// are recognised by immutable identity, never by email, because an Administrator can change any account's email.
// Every manifest that registers a sample person contributes here, and the rule applies in every environment since
// these identifiers never occur in real data.
public static class SampleAccountProtection
{
    private static readonly HashSet<Guid> StaffIds =
    [
        .. DevelopmentFixtureManifest.Staff.Select(item => item.Id),
        .. DevelopmentFixtureManifest.ReliefTechnicians.Select(item => item.Id),
        .. DevelopmentFixtureManifest.AdditionalAgents.Select(item => item.Id),
        .. DevelopmentLifecycleManifest.Staff.Select(item => item.Id)
    ];

    private static readonly HashSet<Guid> CustomerRegistrationKeys =
    [
        .. DevelopmentFixtureManifest.Customers.Select(item => item.Registration.RegistrationKey),
        .. DevelopmentTestDriveVolumeManifest.All.Select(item => item.Customer.Registration.RegistrationKey),
        .. DevelopmentSaleVolumeManifest.Customers.Select(item => item.Registration.RegistrationKey)
    ];

    public static bool IsProtectedStaff(Guid userId) => StaffIds.Contains(userId);

    public static bool IsProtectedCustomer(Guid registrationKey) => CustomerRegistrationKeys.Contains(registrationKey);
}
