using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationDepositRequestStore(DealershipDbContext database, ReservationDepositToken tokens)
    : IReservationDepositRequestStore
{
    public async Task<ReservationDepositRequest?> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (!ReservationDepositToken.TryReadId(token, out var requestId))
        {
            return null;
        }

        var request = await database.ReservationDepositRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        return request is not null && tokens.Verify(token, request) ? request : null;
    }

    public Task<ReservationDepositRequest?> FindAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return database.ReservationDepositRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
    }

    public async Task<bool> CancelAsync(Guid requestId, DateTimeOffset cancelledAt, CancellationToken cancellationToken)
    {
        var changed = await database.ReservationDepositRequests.Where(item => item.Id == requestId &&
                item.ConfirmedAt == null && item.CancelledAt == null && item.ExpiresAt > cancelledAt)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CancelledAt, cancelledAt), cancellationToken);
        return changed == 1;
    }

    public async Task MarkConfirmedAsync(Guid requestId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
    {
        await database.ReservationDepositRequests.Where(item => item.Id == requestId && item.ConfirmedAt == null && item.CancelledAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ConfirmedAt, confirmedAt), cancellationToken);
    }
}
