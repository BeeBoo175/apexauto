namespace Dealership.Application.Accounts;

// What a screen shows for a person whose identity was removed: the row and its id are kept, the name is NULL.
// Reads substitute these labels instead of a fabricated name, an email or an identifier fragment.
public static class PersonLabels
{
    public const string FormerCustomer = "Former customer";

    public const string FormerEmployee = "Former employee";
}
