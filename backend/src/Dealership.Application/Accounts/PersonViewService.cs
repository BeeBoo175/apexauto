namespace Dealership.Application.Accounts;

public enum PersonViewStatus
{
    Found,
    NotFound,
    Forbidden
}

public sealed record PersonView(PersonViewStatus Status, Person? Person = null);

// Staff see colleagues in full and customers by name only. A customer sees only the staff currently handling
// their records, by picture, name and branch, with no directory.
public sealed class PersonViewService(IAccountProfileQuery profiles, IPersonQuery people,
    IRecordHandlerQuery handlers)
{
    public async Task<PersonView> GetAsync(Guid viewerId, Guid personId, CancellationToken cancellationToken)
    {
        var viewer = await profiles.GetProfileAsync(viewerId, cancellationToken);

        if (viewer is null)
        {
            return new PersonView(PersonViewStatus.Forbidden);
        }

        if (viewer.Role == AccountRoles.Customer)
        {
            return await GetForCustomerAsync(viewerId, personId, cancellationToken);
        }

        var person = await people.FindAsync(personId, cancellationToken);

        if (person is null)
        {
            return new PersonView(PersonViewStatus.NotFound);
        }

        return new PersonView(PersonViewStatus.Found, Shown(person));
    }

    private async Task<PersonView> GetForCustomerAsync(Guid customerId, Guid staffId,
        CancellationToken cancellationToken)
    {
        // Not-handling and unknown accounts answer alike, so a customer cannot probe for staff accounts.
        if (!await handlers.HandlesCustomerAsync(staffId, customerId, cancellationToken))
        {
            return new PersonView(PersonViewStatus.Forbidden);
        }

        var staff = await people.FindAsync(staffId, cancellationToken);

        if (staff is null || staff.Role == AccountRoles.Customer)
        {
            return new PersonView(PersonViewStatus.Forbidden);
        }

        return new PersonView(PersonViewStatus.Found,
            staff with { Username = string.Empty, Email = null, Mobile = null });
    }

    private static Person Shown(Person person)
    {
        // A customer's own record access, not the directory, decides contact details; hide them until that exists.
        return person.Role == AccountRoles.Customer || !person.IsActive
            ? person with { Email = null, Mobile = null }
            : person;
    }
}
