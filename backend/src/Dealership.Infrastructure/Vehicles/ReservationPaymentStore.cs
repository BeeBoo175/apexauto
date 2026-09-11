using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationPaymentStore(DealershipDbContext database, MailDeliverySignal mail) : IReservationPaymentStore
{
    public Task<ReservationDepositPayment?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return database.ReservationDepositPayments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
    }

    public Task<ReservationDepositPayment?> FindByBillAsync(string billId, CancellationToken cancellationToken)
    {
        return database.ReservationDepositPayments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BillId == billId, cancellationToken);
    }

    public async Task<DepositPayer?> FindPayerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var rows = await (
            from profile in database.CustomerProfiles.AsNoTracking()
            join user in database.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.UserId == customerId
            select new { profile.LegalName, user.Email }).ToListAsync(cancellationToken);
        return rows.Count == 1 && rows[0].Email is { Length: > 0 } email ? new(rows[0].LegalName, email) : null;
    }

    public async Task<DepositPaymentAddResult> AddAsync(ReservationDepositPayment payment,
        CancellationToken cancellationToken)
    {
        // The request row is locked first, the same row a closing customer's deposit lock takes, so the recheck below
        // and the insert see one state of the request and its customer.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var request = await database.ReservationDepositRequests.FromSqlInterpolated($"""
            SELECT * FROM "ReservationDepositRequests" WHERE "Id" = {payment.RequestId} FOR UPDATE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var open = request is not null && request.IsPending(now) && !await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == request.CustomerId && user.ClosedAt != null, cancellationToken);
        if (!open)
        {
            return DepositPaymentAddResult.Refused;
        }

        database.ReservationDepositPayments.Add(payment);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DepositPaymentAddResult.Added;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.Entry(payment).State = EntityState.Detached;
            return DepositPaymentAddResult.Exists;
        }
    }

    public Task MarkBillDeletedAsync(Guid requestId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return database.ReservationDepositPayments.Where(item => item.RequestId == requestId &&
                item.State == DepositPaymentState.Pending && item.BillDeletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.BillDeletedAt, now), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ReleasableRequestsAsync(DateTimeOffset since, DateTimeOffset now, int limit,
        CancellationToken cancellationToken)
    {
        return await (
            from payment in database.ReservationDepositPayments.AsNoTracking()
            join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
            where payment.State == DepositPaymentState.Pending && payment.BillDeletedAt == null && request.ConfirmedAt == null &&
                (request.CancelledAt != null && request.CancelledAt >= since ||
                request.CancelledAt == null && request.ExpiresAt <= now && request.ExpiresAt >= since)
            orderby request.ExpiresAt
            select request.Id).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<bool> RecordPaidAsync(string billId, int paidAmountSen, bool reserved, DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        var payment = await database.ReservationDepositPayments
            .SingleOrDefaultAsync(item => item.BillId == billId, cancellationToken);
        if (payment is null || !payment.RecordPaid(paidAmountSen, reserved, paidAt))
        {
            return false;
        }

        // The pending state in the condition makes the first of two simultaneous deliveries the only writer.
        var state = payment.State;
        var changed = await database.ReservationDepositPayments.Where(item => item.BillId == billId &&
                item.State == DepositPaymentState.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, state)
                .SetProperty(item => item.PaidAt, paidAt).SetProperty(item => item.PaidAmountSen, paidAmountSen),
                cancellationToken);
        database.Entry(payment).State = EntityState.Detached;
        if (changed == 1 && state == DepositPaymentState.PaidUnmatched && paidAmountSen > 0)
        {
            // Only the winning delivery queues the notice that tells the customer to claim the refund.
            database.ReservationUnmatchedPaymentMailOutbox.Add(new(payment.RequestId, paidAt));
            await database.SaveChangesAsync(cancellationToken);
            mail.Request();
        }

        return changed == 1;
    }
}
