namespace Dealership.Application.Accounts;

public enum PersonViewStatus
{
    Found,
    NotFound,
    Forbidden
}

public sealed record PersonView(PersonViewStatus Status, Person? Person = null);

// Staff see colleagues in full and customers by name only; customers see no directory.
// Showing a customer the staff who handle their record arrives with the handler display.
public sealed class PersonViewService(IAccountProfileQuery profiles, IPersonQuery people)
{
    public async Task<PersonView> GetAsync(Guid viewerId, Guid personId, CancellationToken cancellationToken)
    {
        var viewer = await profiles.GetProfileAsync(viewerId, cancellationToken);

        if (viewer is null || viewer.Role == AccountRoles.Customer)
        {
            return new PersonView(PersonViewStatus.Forbidden);
        }

        var person = await people.FindAsync(personId, cancellationToken);

        if (person is null)
        {
            return new PersonView(PersonViewStatus.NotFound);
        }

        return new PersonView(PersonViewStatus.Found, Shown(person));
    }

    private static Person Shown(Person person)
    {
        // A customer's own record access, not the directory, decides contact details; hide them until that exists.
        return person.Role == AccountRoles.Customer || !person.IsActive
            ? person with { Email = null, Mobile = null }
            : person;
    }
}
