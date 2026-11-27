using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

// The records that stop a customer closing their account, read in one statement so every blocker shares a snapshot.
// A finished, cancelled or missed record never blocks, and nothing is cancelled or settled on the customer's behalf.
internal static class CustomerClosureBlockers
{
    private static readonly SalesOrderStatus[] OpenSaleStates =
    [
        SalesOrderStatus.Handling, SalesOrderStatus.PendingApproval, SalesOrderStatus.Approved, SalesOrderStatus.Settled
    ];

    public static async Task<IReadOnlyList<ClosureBlocker>> ReadAsync(DealershipDbContext database, Guid customerId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = AppointmentDay.StartUtc(now);
        var appointments = from appointment in database.IntakeAppointments.AsNoTracking()
                           where appointment.OwnerId == customerId && appointment.CancelledAt == null &&
                               appointment.EndsAt > now &&
                               !database.AppointmentCheckIns.Any(checkIn => checkIn.AppointmentId == appointment.Id)
                           select new BlockerRow
                           {
                               Type = ClosureBlockerTypes.IntakeAppointment,
                               Reference = appointment.RegistrationPlate,
                               Status = "Booked",
                               BranchId = appointment.BranchId
                           };
        var testDrives = from booking in database.TestDriveBookings.AsNoTracking()
                         let started = database.TestDriveStarts.Any(start => start.BookingId == booking.Id)
                         where booking.CustomerId == customerId &&
                             !database.TestDriveCancellations.Any(cancel => cancel.BookingId == booking.Id) &&
                             !database.TestDriveOutcomes.Any(outcome => outcome.BookingId == booking.Id) &&
                             (started || booking.StartsAt >= today)
                         select new BlockerRow
                         {
                             Type = ClosureBlockerTypes.TestDrive,
                             Reference = booking.Id.ToString().Substring(0, 8),
                             Status = started ? "In progress" : "Booked",
                             BranchId = booking.BranchId
                         };
        var requests = from request in database.ReservationDepositRequests.AsNoTracking()
                           .Where(ReservationDepositRequest.PendingAt(now))
                       join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
                       where request.CustomerId == customerId
                       select new BlockerRow
                       {
                           Type = ClosureBlockerTypes.DepositRequest,
                           Reference = vehicle.RegistrationPlate,
                           Status = "Holding the car",
                           BranchId = request.BranchId
                       };
        // A reservation that already became a sale in progress is reported once, as the purchase.
        var reservations = from reservation in database.Reservations.AsNoTracking()
                           where reservation.CustomerId == customerId && reservation.IsActive &&
                               !database.SalesOrders.Any(sale => sale.Id == reservation.Id &&
                                   OpenSaleStates.Contains(sale.Status))
                           select new BlockerRow
                           {
                               Type = ClosureBlockerTypes.Reservation,
                               Reference = reservation.Number,
                               Status = "Reserved",
                               BranchId = reservation.BranchId
                           };
        var sales = from sale in database.SalesOrders.AsNoTracking()
                    where sale.CustomerId == customerId && OpenSaleStates.Contains(sale.Status)
                    select new BlockerRow
                    {
                        Type = ClosureBlockerTypes.Sale,
                        Reference = sale.OrderNumber ?? sale.Id.ToString().Substring(0, 8),
                        Status = sale.Status.ToString(),
                        BranchId = sale.BranchId
                    };
        var bills = from payment in database.ReservationDepositPayments.AsNoTracking()
                    join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
                    join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
                    where request.CustomerId == customerId && payment.State == DepositPaymentState.Pending &&
                        payment.BillDeletedAt == null
                    select new BlockerRow
                    {
                        Type = ClosureBlockerTypes.DepositBill,
                        Reference = vehicle.RegistrationPlate,
                        Status = "Bill open",
                        BranchId = request.BranchId
                    };
        // SQL twin: RetentionRules.MoneyOwed; the paid-deposit conditions below must change together with it.
        var refunds = from payment in database.ReservationDepositPayments.AsNoTracking()
                      join request in database.ReservationDepositRequests.AsNoTracking() on payment.RequestId equals request.Id
                      join vehicle in database.Vehicles.AsNoTracking() on request.VehicleId equals vehicle.Id
                      where request.CustomerId == customerId &&
                          ((payment.State == DepositPaymentState.PaidUnmatched && payment.PaidAmountSen > 0) ||
                              (payment.State == DepositPaymentState.Paid &&
                                  database.ReservationReleases.Any(release => release.ReservationId == payment.RequestId))) &&
                          !database.ReservationRefunds.Any(refund => refund.PaymentRequestId == payment.RequestId &&
                              refund.State == RefundState.Completed)
                      select new BlockerRow
                      {
                          Type = ClosureBlockerTypes.Refund,
                          Reference = vehicle.RegistrationPlate,
                          Status = database.ReservationRefunds.Any(refund => refund.PaymentRequestId == payment.RequestId &&
                              refund.State == RefundState.Processing)
                              ? "Refund processing"
                              : "Refund owed",
                          BranchId = request.BranchId
                      };

        var rows = await appointments.Concat(testDrives).Concat(requests).Concat(reservations).Concat(sales)
            .Concat(bills).Concat(refunds).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var branchIds = rows.Select(row => row.BranchId).Distinct().ToArray();
        var branches = await database.Branches.AsNoTracking().Where(branch => branchIds.Contains(branch.Id))
            .ToDictionaryAsync(branch => branch.Id, branch => branch.Name, cancellationToken);
        return [.. rows.Select(row => new ClosureBlocker(row.Type, row.Reference.ToUpperInvariant(), StatusLabel(row),
            branches.GetValueOrDefault(row.BranchId, string.Empty), Resolution(row.Type)))];
    }

    // Sale statuses are projected raw because the label helper cannot be translated to SQL.
    private static string StatusLabel(BlockerRow row)
    {
        if (row.Type != ClosureBlockerTypes.Sale)
        {
            return row.Status;
        }

        switch (row.Status)
        {
            case nameof(SalesOrderStatus.Settled):
                return "Awaiting collection";
            case nameof(SalesOrderStatus.PendingApproval):
                return "Pending approval";
            default:
                return row.Status;
        }
    }

    private static string Resolution(string type) => type switch
    {
        ClosureBlockerTypes.IntakeAppointment => "Cancel the booking or attend it, then close the account.",
        ClosureBlockerTypes.TestDrive => "Cancel the test drive or take it, then close the account.",
        ClosureBlockerTypes.DepositRequest => "Pay the deposit or cancel the request from your account, or ask the branch if it was made at the showroom.",
        ClosureBlockerTypes.Reservation => "Cancel the reservation or complete the purchase.",
        ClosureBlockerTypes.Sale => "Ask the branch to finish or cancel the purchase.",
        ClosureBlockerTypes.DepositBill => "Close again in a moment while the unpaid bill is removed.",
        _ => "Claim the refund from Account details and wait for the bank to confirm it."
    };

    private sealed class BlockerRow
    {
        public string Type { get; init; } = string.Empty;

        public string Reference { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public Guid BranchId { get; init; }
    }
}
