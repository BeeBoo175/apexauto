using Dealership.Infrastructure.Development;

namespace Dealership.UnitTests;

public sealed class SampleAccountProtectionTests
{
    [Fact]
    public void EveryManifestPersonIsProtectedAndAnOrdinaryIdentityIsNot()
    {
        Assert.All(DevelopmentFixtureManifest.Staff, item => Assert.True(SampleAccountProtection.IsProtectedStaff(item.Id)));
        Assert.All(DevelopmentFixtureManifest.ReliefTechnicians,
            item => Assert.True(SampleAccountProtection.IsProtectedStaff(item.Id)));
        Assert.All(DevelopmentFixtureManifest.AdditionalAgents,
            item => Assert.True(SampleAccountProtection.IsProtectedStaff(item.Id)));
        Assert.All(DevelopmentLifecycleManifest.Staff, item => Assert.True(SampleAccountProtection.IsProtectedStaff(item.Id)));
        Assert.All(DevelopmentFixtureManifest.Customers,
            item => Assert.True(SampleAccountProtection.IsProtectedCustomer(item.Registration.RegistrationKey)));
        Assert.All(DevelopmentTestDriveVolumeManifest.All, item => Assert.True(
            SampleAccountProtection.IsProtectedCustomer(item.Customer.Registration.RegistrationKey)));
        Assert.All(DevelopmentSaleVolumeManifest.Customers,
            item => Assert.True(SampleAccountProtection.IsProtectedCustomer(item.Registration.RegistrationKey)));
        Assert.False(SampleAccountProtection.IsProtectedCustomer(Guid.NewGuid()));
        Assert.False(SampleAccountProtection.IsProtectedStaff(Guid.NewGuid()));
    }
}
