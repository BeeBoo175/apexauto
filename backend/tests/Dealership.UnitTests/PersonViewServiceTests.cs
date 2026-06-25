using Dealership.Application.Accounts;

namespace Dealership.UnitTests;

public sealed class PersonViewServiceTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid HandlerId = Guid.NewGuid();

    [Fact]
    public async Task CustomerSeesHandlingStaffByPictureNameAndBranchOnly()
    {
        var service = Service(handles: true);

        var view = await service.GetAsync(CustomerId, HandlerId, CancellationToken.None);

        Assert.Equal(PersonViewStatus.Found, view.Status);
        Assert.Equal("Amir Hakim", view.Person!.Name);
        Assert.Equal("Petaling Jaya", view.Person.BranchName);
        Assert.Equal(PictureId, view.Person.PictureId);
        Assert.Equal(string.Empty, view.Person.Username);
        Assert.Null(view.Person.Email);
        Assert.Null(view.Person.Mobile);
    }

    [Fact]
    public async Task CustomerCannotSeeStaffWhoDoNotHandleTheirRecord()
    {
        var service = Service(handles: false);

        var view = await service.GetAsync(CustomerId, HandlerId, CancellationToken.None);

        Assert.Equal(PersonViewStatus.Forbidden, view.Status);
    }

    [Fact]
    public async Task CustomerCannotSeeAnotherCustomerEvenWhenAskedAsAHandler()
    {
        var service = Service(handles: true, targetRole: AccountRoles.Customer);

        var view = await service.GetAsync(CustomerId, HandlerId, CancellationToken.None);

        Assert.Equal(PersonViewStatus.Forbidden, view.Status);
    }

    private static readonly Guid PictureId = Guid.NewGuid();

    private static PersonViewService Service(bool handles, string targetRole = AccountRoles.SalesAgent)
    {
        return new PersonViewService(new Profiles(), new People(targetRole), new Handlers(handles));
    }

    private sealed class Profiles : IAccountProfileQuery
    {
        public Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<AccountProfile?>(new AccountProfile(userId, AccountRoles.Customer, "Customer",
                "customer.one", "customer@example.invalid", true, null, null, null, null));
        }
    }

    private sealed class People(string role) : IPersonQuery
    {
        public Task<Person?> FindAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Person?>(new Person(userId, role, "Amir Hakim", "amir.hakim", true,
                Guid.NewGuid(), "Petaling Jaya", "amir@example.invalid", "0123456789", PictureId));
        }
    }

    private sealed class Handlers(bool handles) : IRecordHandlerQuery
    {
        public Task<bool> HandlesCustomerAsync(Guid staffId, Guid customerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(handles);
        }
    }
}
