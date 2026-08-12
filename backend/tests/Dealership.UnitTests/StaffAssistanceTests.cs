using Dealership.Application.Accounts;

namespace Dealership.UnitTests;

public sealed class StaffAssistanceTests
{
    private static readonly Guid Bayan = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Glenmarie = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Theory]
    [InlineData(AccountRoles.SalesAgent, true)]
    [InlineData(AccountRoles.BranchManager, true)]
    [InlineData(AccountRoles.GeneralManager, true)]
    [InlineData(AccountRoles.WorkshopTechnician, false)]
    [InlineData(AccountRoles.Administrator, false)]
    [InlineData(AccountRoles.Customer, false)]
    public void OnlySalesRolesMayActForACustomer(string role, bool allowed)
    {
        Assert.Equal(allowed, StaffAssistanceRoles.Allows(role));
    }

    [Fact]
    public void BranchRolesActAtTheirOwnBranchAndTheGeneralManagerActsAnywhere()
    {
        var agent = new AssistingStaff(Guid.NewGuid(), AccountRoles.SalesAgent, Bayan);
        var general = new AssistingStaff(Guid.NewGuid(), AccountRoles.GeneralManager, null);

        Assert.True(agent.CanActAt(Bayan));
        Assert.False(agent.CanActAt(Glenmarie));
        Assert.True(general.CanActAt(Glenmarie));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  a ")]
    public void ShortSearchesAreRejected(string? search)
    {
        Assert.Null(CustomerLookupQuery.Parse(search));
    }

    [Fact]
    public void SearchIsTrimmedAndKept()
    {
        Assert.Equal("aisyah", CustomerLookupQuery.Parse("  aisyah ")?.Search);
    }
}
