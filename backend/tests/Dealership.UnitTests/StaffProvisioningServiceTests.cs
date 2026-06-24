using Dealership.Application.Accounts;

namespace Dealership.UnitTests;

public sealed class StaffProvisioningServiceTests
{
    [Theory]
    [InlineData(AccountRoles.Customer, false)]
    [InlineData(AccountRoles.SalesAgent, false)]
    [InlineData(AccountRoles.Administrator, true)]
    public async Task InvalidAssignmentsAreRejectedBeforePersistence(string role, bool assignedBranch)
    {
        var service = new StaffProvisioningService(new UnexpectedProvisioningStore());
        var request = new StaffProvisioningRequest(Guid.NewGuid(), "aina.sofia@example.invalid", "Password123!",
            role, assignedBranch ? Guid.NewGuid() : null, "Aina Sofia", "0123456710");
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionStaffAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("   ", "0123456710")]
    [InlineData("Aina Sofia", "   ")]
    [InlineData("Aina Sofia", "0123456789012345678901234567890")]
    public async Task InvalidProfileFieldsAreRejectedBeforePersistence(string name, string mobile)
    {
        var service = new StaffProvisioningService(new UnexpectedProvisioningStore());
        var request = new StaffProvisioningRequest(Guid.NewGuid(), "aina.sofia@example.invalid", "Password123!",
            AccountRoles.Administrator, null, name, mobile);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionStaffAsync(request, CancellationToken.None));
    }

    private sealed class UnexpectedProvisioningStore : IStaffProvisioningStore
    {
        public Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid staff input must not reach persistence.");
        }
    }
}
