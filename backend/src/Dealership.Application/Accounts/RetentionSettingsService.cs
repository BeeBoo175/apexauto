using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

public sealed class RetentionSettingsService(IRetentionSettingsStore store, TimeProvider clock)
{
    public Task<RetentionSettingsResult> ReadAsync(Guid actorId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty
            ? Task.FromResult(new RetentionSettingsResult(RetentionSettingsStatus.Forbidden))
            : store.ReadAsync(actorId, cancellationToken);

    public Task<RetentionSettingsResult> UpdateAsync(Guid actorId, RetentionSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Task.FromResult(new RetentionSettingsResult(RetentionSettingsStatus.Forbidden));
        }

        if (request.ExpectedVersion <= 0 || !RetentionSettings.ValidValues(request.SecurityArtifactDays,
            request.MailDetailDays, request.InvitationDays, request.BusinessEvidenceYears))
        {
            return Task.FromResult(new RetentionSettingsResult(RetentionSettingsStatus.Invalid));
        }

        return store.UpdateAsync(actorId, request, clock.GetUtcNow(), cancellationToken);
    }
}
