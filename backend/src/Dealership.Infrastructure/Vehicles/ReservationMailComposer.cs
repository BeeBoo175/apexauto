using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationMailComposer
{

    public static OutboundMail Confirmation(Reservation reservation)
    {
        var layout = new MailLayout(
            "Your reservation receipt",
            ["Thank you for reserving with ApexAuto. This receipt records your original confirmation."],
            [
                new MailDetail("Reference", reservation.Number),
                new MailDetail("Confirmed", MailFormat.LocalTime(reservation.CreatedAt)),
                new MailDetail("Hold deadline", MailFormat.LocalTime(reservation.ExpiresAt)),
                new MailDetail("Agreed total", MailFormat.Money(reservation.Price.Total)),
                new MailDetail("Promo", reservation.PromoCode ?? "None"),
                new MailDetail("Refundable deposit", MailFormat.Money(reservation.Deposit))
            ],
            ["Check your account for the current reservation and refund status. The deposit is refundable on cancellation or expiry."]);
        return new OutboundMail(reservation.Buyer.Email, "ApexAuto reservation receipt",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
