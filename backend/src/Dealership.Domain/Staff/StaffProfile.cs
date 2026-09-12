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

    // Erasure removes both personal fields together and marks the state; the row stays as the identity reference.
    public void Erase(DateTimeOffset now)
    {
        Name = null;
        Mobile = null;
        ErasedAt = now;
    }

    public Guid UserId { get; private set; }

    // NULL together with the mobile once the employee is erased; ErasedAt marks that state.
    public string? Name { get; private set; }

    // A work number, visible to every staff member.
    public string? Mobile { get; private set; }

    public DateTimeOffset? ErasedAt { get; private set; }
}
