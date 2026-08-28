namespace Dealership.Domain.Branches;

public sealed class Branch
{
    public const int NameMaximumLength = 100;
    public const int AddressMaximumLength = 500;
    public const int CityMaximumLength = 60;
    public const int StateMaximumLength = 60;
    public const int PhoneMaximumLength = 30;
    public const int ShortCodeMaximumLength = 4;

    private Branch()
    {
    }

    public Branch(Guid id, string name, string address, string city, string state, string phone,
        string shortCode, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Branch identity and UTC creation time are required.");
        }

        Name = RequiredText(name, NameMaximumLength);
        Address = RequiredText(address, AddressMaximumLength);
        City = RequiredText(city, CityMaximumLength);
        State = RequiredText(state, StateMaximumLength);
        Phone = RequiredText(phone, PhoneMaximumLength);
        ShortCode = RequiredText(shortCode, ShortCodeMaximumLength).ToUpperInvariant();
        Id = id;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    // The label calendars and tables show where the full name would not fit.
    public string ShortCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string RequiredText(string value, int maximumLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > maximumLength)
        {
            throw new ArgumentException("Branch details are required within their length limits.");
        }

        return trimmed;
    }
}
