using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dealership.Infrastructure.Vehicles;

public sealed class ReservationRefundStore(DealershipDbContext database) : IReservationRefundStore
{
    public async Task<IReadOnlyList<RefundView>> ListAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var due = await DueRows(customerId).ToListAsync(cancellationToken);
        var ids = due.Select(item => item.PaymentRequestId).ToList();
        var existing = await database.ReservationRefunds.AsNoTracking()
            .Where(item => ids.Contains(item.PaymentRequestId)).ToListAsync(cancellationToken);
        return due.OrderByDescending(item => item.DueAt).Select(item =>
        {
            var refund = existing.SingleOrDefault(row => row.PaymentRequestId == item.PaymentRequestId);
            return new RefundView(item.PaymentRequestId, item.AmountSen, item.Reason,
                $"{item.Year} {item.Make} {item.Model} ({item.Plate})",
                refund is null ? "due" : ReservationRefundService.StateName(refund.State), refund?.BankCode,
                refund?.AccountLast4, item.DueAt);
        }).ToList();
    }

    public async Task<RefundDue?> FindDueAsync(Guid customerId, Guid paymentRequestId, CancellationToken cancellationToken)
    {
        var row = await DueRows(customerId).Where(item => item.PaymentRequestId == paymentRequestId)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new RefundDue(row.PaymentRequestId, customerId, row.AmountSen, row.Reason);
    }

    public Task<ReservationRefund?> FindAsync(Guid paymentRequestId, CancellationToken cancellationToken)
    {
        return database.ReservationRefunds.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PaymentRequestId == paymentRequestId, cancellationToken);
    }

    public Task<ReservationRefund?> FindByPayoutAsync(string payoutId, CancellationToken cancellationToken)
    {
        return database.ReservationRefunds.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PayoutId == payoutId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ProcessingPayoutsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await database.ReservationRefunds.AsNoTracking()
            .Where(item => item.CustomerId == customerId && item.State == RefundState.Processing && item.PayoutId != null)
            .Select(item => item.PayoutId!).ToListAsync(cancellationToken);
    }

    public async Task<bool> AddAsync(ReservationRefund refund, CancellationToken cancellationToken)
    {
        database.ReservationRefunds.Add(refund);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            database.Entry(refund).State = EntityState.Detached;
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.Entry(refund).State = EntityState.Detached;
            return false;
        }
    }

    public async Task SaveAsync(ReservationRefund refund, CancellationToken cancellationToken)
    {
        database.ReservationRefunds.Update(refund);
        await database.SaveChangesAsync(cancellationToken);
        database.Entry(refund).State = EntityState.Detached;
    }

    // A paid deposit is refundable once its reservation was released, or at once when it could not be matched.
    private IQueryable<DueRow> DueRows(Guid customerId)
    {
        return
            from payment in database.ReservationDepositPayments.AsNoTracking()
            join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
            join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
            where request.CustomerId == customerId
            let released = database.ReservationReleases.Where(item => item.ReservationId == payment.RequestId)
                .Select(item => (DateTimeOffset?)item.RecordedAt).FirstOrDefault()
            where payment.State == DepositPaymentState.PaidUnmatched && payment.PaidAmountSen > 0 ||
                payment.State == DepositPaymentState.Paid && released != null
            select new DueRow
            {
                PaymentRequestId = payment.RequestId,
                AmountSen = payment.State == DepositPaymentState.Paid ? payment.AmountSen : payment.PaidAmountSen!.Value,
                Reason = released != null ? RefundDueReason.ReservationReleased : RefundDueReason.PaymentUnmatched,
                DueAt = released ?? payment.PaidAt!.Value,
                Year = vehicle.ManufactureYear,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Plate = vehicle.RegistrationPlate
            };
    }

    private sealed class DueRow
    {
        public Guid PaymentRequestId { get; init; }
        public int AmountSen { get; init; }
        public RefundDueReason Reason { get; init; }
        public DateTimeOffset DueAt { get; init; }
        public int Year { get; init; }
        public string Make { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string Plate { get; init; } = string.Empty;
    }
}
