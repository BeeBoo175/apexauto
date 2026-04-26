using Dealership.Application.Accounts;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class PurchaseSettingsAdministrationStore(DealershipDbContext database) : IPurchaseSettingsAdministrationStore
{
    public async Task<PurchaseSettingsAdministrationResult> ReadAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(actorId, cancellationToken))
        {
            return new(PurchaseSettingsAdministrationStatus.Forbidden);
        }

        var settings = await database.PurchaseSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return settings is null
            ? new(PurchaseSettingsAdministrationStatus.NotFound)
            : new(PurchaseSettingsAdministrationStatus.Succeeded, PurchaseSettingsView.From(settings));
    }

    public async Task<PurchaseSettingsAdministrationResult> UpdateAsync(Guid actorId,
        PurchaseSettingsUpdateRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actorId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (actor is not { IsActive: true, Role: AccountRoles.Administrator })
        {
            return new(PurchaseSettingsAdministrationStatus.Forbidden);
        }

        var settings = await database.PurchaseSettings.FromSqlInterpolated(
            $"SELECT * FROM \"PurchaseSettings\" WHERE \"Id\" = 1 FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return new(PurchaseSettingsAdministrationStatus.NotFound);
        }

        if (settings.Version != request.ExpectedVersion)
        {
            return new(PurchaseSettingsAdministrationStatus.Conflict, PurchaseSettingsView.From(settings));
        }

        // A lagging application clock must not stamp a change earlier than the one it replaces.
        var changedAt = now < settings.UpdatedAtUtc ? settings.UpdatedAtUtc : now;
        settings.Update(request.ProcessingFee, request.SstRate, request.MaxAgentDiscountAmount,
            request.ReservationHoldHours, actorId, changedAt);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PurchaseSettingsAdministrationStatus.Succeeded, PurchaseSettingsView.From(settings));
    }

    private Task<bool> IsAdministratorAsync(Guid actorId, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive &&
            user.Role == AccountRoles.Administrator, cancellationToken);
}
