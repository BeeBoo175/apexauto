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

    public async Task<bool> AddAsync(ReservationDepositPayment payment, CancellationToken cancellationToken)
    {
        database.ReservationDepositPayments.Add(payment);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.Entry(payment).State = EntityState.Detached;
            return false;
        }
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
