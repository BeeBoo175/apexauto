using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationMailComposer
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static OutboundMail Confirmation(Reservation reservation)
    {
        var confirmedAt = TimeZoneInfo.ConvertTime(reservation.CreatedAt, Malaysia);
        var expiresAt = TimeZoneInfo.ConvertTime(reservation.ExpiresAt, Malaysia);
        var body = FormattableString.Invariant($"""
            Your ApexAuto reservation receipt
            Reference: {reservation.Number}
            Confirmed: {confirmedAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)
            Hold deadline: {expiresAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)
            Agreed total: RM {reservation.Price.Total:F2}
            Promo: {reservation.PromoCode ?? "None"}
            Simulated refundable deposit: RM {reservation.Deposit:F2}
            This receipt records the original confirmation. Check your account for the current reservation and refund status.
            """);
        return new OutboundMail(reservation.Buyer.Email, "ApexAuto reservation receipt", body);
    }
}
