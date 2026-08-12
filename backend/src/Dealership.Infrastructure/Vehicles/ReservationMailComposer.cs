using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationMailComposer
{

    public static OutboundMail Confirmation(Reservation reservation)
    {
        var confirmedAt = TimeZoneInfo.ConvertTime(reservation.CreatedAt, DealershipLocale.Malaysia);
        var expiresAt = TimeZoneInfo.ConvertTime(reservation.ExpiresAt, DealershipLocale.Malaysia);
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
        var html = MailTemplate.Render(new MailLayout(
            "Your reservation receipt",
            ["Thank you for reserving with ApexAuto. This receipt records your original confirmation."],
            [
                new MailDetail("Reference", reservation.Number),
                new MailDetail("Confirmed", $"{confirmedAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)"),
                new MailDetail("Hold deadline", $"{expiresAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)"),
                new MailDetail("Agreed total", $"RM {reservation.Price.Total:N2}"),
                new MailDetail("Promo", reservation.PromoCode ?? "None"),
                new MailDetail("Refundable deposit", $"RM {reservation.Deposit:N2}")
            ],
            ["Check your account for the current reservation and refund status. The deposit is refundable on cancellation or expiry."]));
        return new OutboundMail(reservation.Buyer.Email, "ApexAuto reservation receipt", body, html);
    }
}
