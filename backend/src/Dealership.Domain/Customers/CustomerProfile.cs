namespace Dealership.Domain.Customers;

public sealed class CustomerProfile
{
    public const int NricLength = 12;
    public const int LegalNameMaximumLength = 200;
    public const int MobileMaximumLength = 30;
    public const int AddressMaximumLength = 1000;

    private CustomerProfile()
    {
    }

    public CustomerProfile(Guid userId, Guid registrationKey, string legalName, string nric,
        string mobile, string residentialAddress)
    {
        if (userId == Guid.Empty || registrationKey == Guid.Empty)
        {
            throw new ArgumentException("Customer and registration identities must be non-empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobile);
        ArgumentException.ThrowIfNullOrWhiteSpace(residentialAddress);

        if (legalName.Length > LegalNameMaximumLength || mobile.Length > MobileMaximumLength ||
            residentialAddress.Length > AddressMaximumLength || !IsValidNric(nric))
        {
            throw new ArgumentException("Customer profile fields are invalid.");
        }

        UserId = userId;
        RegistrationKey = registrationKey;
        LegalName = legalName;
        Nric = nric;
        Mobile = mobile;
        ResidentialAddress = residentialAddress;
    }

    // An administrator's edit; the NRIC and the registration identity never change here.
    public void Update(string legalName, string mobile, string residentialAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobile);
        ArgumentException.ThrowIfNullOrWhiteSpace(residentialAddress);

        if (legalName.Length > LegalNameMaximumLength || mobile.Length > MobileMaximumLength ||
            residentialAddress.Length > AddressMaximumLength)
        {
            throw new ArgumentException("Customer profile fields are invalid.");
        }

        LegalName = legalName;
        Mobile = mobile;
        ResidentialAddress = residentialAddress;
    }

    public Guid UserId { get; private set; }

    public Guid RegistrationKey { get; private set; }

    public string LegalName { get; private set; } = string.Empty;

    public string Nric { get; private set; } = string.Empty;

    public string Mobile { get; private set; } = string.Empty;

    public string ResidentialAddress { get; private set; } = string.Empty;

    public static bool IsValidNric(string? nric)
    {
        // This checks the agreed ASCII shape, not the holder's identity or NRIC authenticity.
        return nric is { Length: NricLength } && nric.All(character => character is >= '0' and <= '9');
    }
}
