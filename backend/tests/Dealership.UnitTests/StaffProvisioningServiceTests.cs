using Dealership.Application.Accounts;
using Dealership.Domain.Branches;

namespace Dealership.UnitTests;

public sealed class StaffProvisioningServiceTests
{
    [Theory]
    [InlineData(AccountRoles.Customer, false)]
    [InlineData(AccountRoles.SalesAgent, false)]
    [InlineData(AccountRoles.Administrator, true)]
    public async Task InvalidAssignmentsAreRejectedBeforePersistence(string role, bool assignedBranch)
    {
        var service = new StaffProvisioningService(new UnexpectedProvisioningStore(), TimeProvider.System);
        var request = new StaffProvisioningRequest(Guid.NewGuid(), "aina.sofia@example.invalid", "Password123!",
            role, assignedBranch ? Guid.NewGuid() : null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionStaffAsync(request, CancellationToken.None));
    }

    private sealed class UnexpectedProvisioningStore : IStaffProvisioningStore
    {
        public Task<ProvisioningResult> ProvisionBranchAsync(Branch branch, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid staff input must not reach persistence.");
        }

        public Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning staff, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid staff input must not reach persistence.");
        }
    }
}
