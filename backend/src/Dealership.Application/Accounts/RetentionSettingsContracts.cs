using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

public sealed record RetentionSettingsUpdateRequest(int SecurityArtifactDays, int MailDetailDays, int InvitationDays,
    int BusinessEvidenceYears, long ExpectedVersion, bool ShorteningConfirmed);

public enum RetentionSettingsStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    ConfirmationRequired
}

// The server owns the approved limits; the editor displays them rather than carrying copies.
public sealed record RetentionSettingsBounds(int MinimumDays, int MaximumDays, int MinimumBusinessYears,
    int MaximumBusinessYears)
{
    public static RetentionSettingsBounds Approved { get; } = new(RetentionSettings.MinimumDays,
        RetentionSettings.MaximumDays, RetentionSettings.MinimumBusinessYears, RetentionSettings.MaximumBusinessYears);
}

public sealed record RetentionSettingsView(int SecurityArtifactDays, int MailDetailDays, int InvitationDays,
    int BusinessEvidenceYears, DateTimeOffset UpdatedAtUtc, long Version)
{
    public RetentionSettingsBounds Bounds { get; } = RetentionSettingsBounds.Approved;

    public static RetentionSettingsView From(RetentionSettings settings) => new(settings.SecurityArtifactDays,
        settings.MailDetailDays, settings.InvitationDays, settings.BusinessEvidenceYears, settings.UpdatedAtUtc,
        settings.Version);
}

public sealed record RetentionSettingsResult(RetentionSettingsStatus Status, RetentionSettingsView? Settings = null);

public interface IRetentionSettingsStore
{
    Task<RetentionSettingsResult> ReadAsync(Guid actorId, CancellationToken cancellationToken);

    Task<RetentionSettingsResult> UpdateAsync(Guid actorId, RetentionSettingsUpdateRequest request,
        DateTimeOffset now, CancellationToken cancellationToken);
}
