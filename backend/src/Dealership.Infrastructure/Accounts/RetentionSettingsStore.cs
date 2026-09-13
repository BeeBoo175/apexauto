using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class RetentionSettingsStore(DealershipDbContext database, RetentionSignal retention) : IRetentionSettingsStore
{
    public async Task<RetentionSettingsResult> ReadAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!await AdministratorScope.IsAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(RetentionSettingsStatus.Forbidden);
        }

        var settings = await database.RetentionSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return settings is null
            ? new(RetentionSettingsStatus.NotFound)
            : new(RetentionSettingsStatus.Succeeded, RetentionSettingsView.From(settings));
    }

    public async Task<RetentionSettingsResult> UpdateAsync(Guid actorId, RetentionSettingsUpdateRequest request,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await AdministratorScope.LockAdministratorAsync(database, actorId, cancellationToken))
        {
            return new(RetentionSettingsStatus.Forbidden);
        }

        var settings = await database.RetentionSettings.FromSqlInterpolated(
            $"SELECT * FROM \"RetentionSettings\" WHERE \"Id\" = 1 FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return new(RetentionSettingsStatus.NotFound);
        }

        if (settings.Version != request.ExpectedVersion)
        {
            return new(RetentionSettingsStatus.Conflict, RetentionSettingsView.From(settings));
        }

        if (!request.ShorteningConfirmed && settings.IsShortenedBy(request.SecurityArtifactDays,
            request.MailDetailDays, request.InvitationDays, request.BusinessEvidenceYears))
        {
            return new(RetentionSettingsStatus.ConfirmationRequired, RetentionSettingsView.From(settings));
        }

        // A lagging application clock must not stamp a change earlier than the one it replaces.
        var changedAt = now < settings.UpdatedAtUtc ? settings.UpdatedAtUtc : now;
        var changes = settings.Update(request.SecurityArtifactDays, request.MailDetailDays, request.InvitationDays,
            request.BusinessEvidenceYears, request.ShorteningConfirmed, actorId, changedAt);
        database.RetentionSettingChanges.AddRange(changes);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // A shorter duration can make rows eligible at once, so the executor looks again after the commit.
        retention.Request();
        return new(RetentionSettingsStatus.Succeeded, RetentionSettingsView.From(settings));
    }
}
