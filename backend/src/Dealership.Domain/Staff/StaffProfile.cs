namespace Dealership.Domain.Staff;

public sealed class StaffProfile
{
    public const int NameMaximumLength = 200;
    public const int MobileMaximumLength = 30;

    private StaffProfile()
    {
    }

    public StaffProfile(Guid userId, string name, string mobile)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Staff identity must be non-empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobile);

        if (name.Length > NameMaximumLength || mobile.Length > MobileMaximumLength)
        {
            throw new ArgumentException("Staff profile fields are invalid.");
        }

        UserId = userId;
        Name = name;
        Mobile = mobile;
    }

    // An administrator's edit; the values were validated by the use case and are checked again here.
    public void Update(string name, string mobile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobile);

        if (name.Length > NameMaximumLength || mobile.Length > MobileMaximumLength)
        {
            throw new ArgumentException("Staff profile fields are invalid.");
        }

        Name = name;
        Mobile = mobile;
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    // A work number, visible to every staff member.
    public string Mobile { get; private set; } = string.Empty;
}
