namespace Dealership.Domain.Accounts;

// Append-only receipt of one changed retention setting: who, when, the values and the shortening confirmation.
public sealed class RetentionSettingChange
{
    private RetentionSettingChange()
    {
    }

    public RetentionSettingChange(string setting, int previousValue, int newValue, long settingsVersion,
        bool shorteningConfirmed, Guid actorId, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        Setting = setting;
        PreviousValue = previousValue;
        NewValue = newValue;
        SettingsVersion = settingsVersion;
        ShorteningConfirmed = shorteningConfirmed;
        ActorId = actorId;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Setting { get; private set; } = string.Empty;

    public int PreviousValue { get; private set; }

    public int NewValue { get; private set; }

    public long SettingsVersion { get; private set; }

    public bool ShorteningConfirmed { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }
}
