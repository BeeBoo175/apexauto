namespace Dealership.Domain.Accounts;

// The four Administrator retention durations. Pending-upload lifetimes and batch sizes are deployment
// configuration, not settings. Business evidence is consumed by the seven-year disposal executor.
public sealed class RetentionSettings
{
    public const int SingletonId = 1;
    public const int DefaultDays = 30;
    public const int MinimumDays = 7;
    public const int MaximumDays = 365;
    public const int DefaultBusinessYears = 7;
    public const int MinimumBusinessYears = 1;
    public const int MaximumBusinessYears = 10;

    public const string SecurityArtifactDaysName = "SecurityArtifactDays";
    public const string MailDetailDaysName = "MailDetailDays";
    public const string InvitationDaysName = "InvitationDays";
    public const string BusinessEvidenceYearsName = "BusinessEvidenceYears";

    private RetentionSettings()
    {
    }

    public RetentionSettings(Guid updatedById, DateTimeOffset updatedAtUtc, int securityArtifactDays = DefaultDays,
        int mailDetailDays = DefaultDays, int invitationDays = DefaultDays,
        int businessEvidenceYears = DefaultBusinessYears)
    {
        ValidateAudit(updatedById, updatedAtUtc);
        ValidateValues(securityArtifactDays, mailDetailDays, invitationDays, businessEvidenceYears);
        Id = SingletonId;
        SecurityArtifactDays = securityArtifactDays;
        MailDetailDays = mailDetailDays;
        InvitationDays = invitationDays;
        BusinessEvidenceYears = businessEvidenceYears;
        UpdatedById = updatedById;
        UpdatedAtUtc = updatedAtUtc;
    }

    public int Id { get; private set; }

    public int SecurityArtifactDays { get; private set; }

    public int MailDetailDays { get; private set; }

    public int InvitationDays { get; private set; }

    public int BusinessEvidenceYears { get; private set; }

    public Guid UpdatedById { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public long Version { get; private set; } = 1;

    // Shortening can make rows eligible immediately, so it needs an explicit confirmation.
    public bool IsShortenedBy(int securityArtifactDays, int mailDetailDays, int invitationDays,
        int businessEvidenceYears) =>
        securityArtifactDays < SecurityArtifactDays || mailDetailDays < MailDetailDays ||
        invitationDays < InvitationDays || businessEvidenceYears < BusinessEvidenceYears;

    // Returns one entry per changed setting so the caller can write the change receipt.
    public IReadOnlyList<RetentionSettingChange> Update(int securityArtifactDays, int mailDetailDays,
        int invitationDays, int businessEvidenceYears, bool shorteningConfirmed, Guid updatedById,
        DateTimeOffset updatedAtUtc)
    {
        ValidateAudit(updatedById, updatedAtUtc);
        ValidateValues(securityArtifactDays, mailDetailDays, invitationDays, businessEvidenceYears);
        if (updatedAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentException("Retention settings cannot be updated before their latest change.");
        }

        if (IsShortenedBy(securityArtifactDays, mailDetailDays, invitationDays, businessEvidenceYears) &&
            !shorteningConfirmed)
        {
            throw new InvalidOperationException("Shortening a retention duration requires confirmation.");
        }

        var nextVersion = Version + 1;
        var changes = new List<RetentionSettingChange>();
        Record(changes, SecurityArtifactDaysName, SecurityArtifactDays, securityArtifactDays, nextVersion,
            shorteningConfirmed, updatedById, updatedAtUtc);
        Record(changes, MailDetailDaysName, MailDetailDays, mailDetailDays, nextVersion, shorteningConfirmed,
            updatedById, updatedAtUtc);
        Record(changes, InvitationDaysName, InvitationDays, invitationDays, nextVersion, shorteningConfirmed,
            updatedById, updatedAtUtc);
        Record(changes, BusinessEvidenceYearsName, BusinessEvidenceYears, businessEvidenceYears, nextVersion,
            shorteningConfirmed, updatedById, updatedAtUtc);

        SecurityArtifactDays = securityArtifactDays;
        MailDetailDays = mailDetailDays;
        InvitationDays = invitationDays;
        BusinessEvidenceYears = businessEvidenceYears;
        UpdatedById = updatedById;
        UpdatedAtUtc = updatedAtUtc;
        Version = nextVersion;
        return changes;
    }

    public static bool ValidValues(int securityArtifactDays, int mailDetailDays, int invitationDays,
        int businessEvidenceYears) =>
        ValidDays(securityArtifactDays) && ValidDays(mailDetailDays) && ValidDays(invitationDays) &&
        businessEvidenceYears is >= MinimumBusinessYears and <= MaximumBusinessYears;

    private static bool ValidDays(int days) => days is >= MinimumDays and <= MaximumDays;

    private static void Record(List<RetentionSettingChange> changes, string setting, int before, int after,
        long version, bool shorteningConfirmed, Guid actorId, DateTimeOffset changedAt)
    {
        if (before == after)
        {
            return;
        }

        changes.Add(new RetentionSettingChange(setting, before, after, version, after < before && shorteningConfirmed,
            actorId, changedAt));
    }

    private static void ValidateAudit(Guid updatedById, DateTimeOffset updatedAtUtc)
    {
        if (updatedById == Guid.Empty || updatedAtUtc.Offset != TimeSpan.Zero ||
            updatedAtUtc == DateTimeOffset.MinValue || updatedAtUtc == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException("Retention settings require an updater and finite UTC timestamp.");
        }
    }

    private static void ValidateValues(int securityArtifactDays, int mailDetailDays, int invitationDays,
        int businessEvidenceYears)
    {
        if (!ValidValues(securityArtifactDays, mailDetailDays, invitationDays, businessEvidenceYears))
        {
            throw new ArgumentOutOfRangeException(nameof(securityArtifactDays),
                "Retention settings are outside the approved bounds.");
        }
    }
}
