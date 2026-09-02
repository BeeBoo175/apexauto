using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationMailComposer
{

    // The payment is the hosted deposit this reservation was made from; a simulated or staff-recorded deposit has none.
    public static OutboundMail Confirmation(Reservation reservation, ReservationDepositPayment? payment)
    {
        var details = new List<MailDetail>
        {
            new("Reference", reservation.Number),
            new("Confirmed", MailFormat.LocalTime(reservation.CreatedAt)),
            new("Hold deadline", MailFormat.LocalTime(reservation.ExpiresAt)),
            new("Agreed total", MailFormat.Money(reservation.Price.Total)),
            new("Promo", reservation.PromoCode ?? "None"),
            new("Refundable deposit", MailFormat.Money(reservation.Deposit))
        };
        if (payment is not null)
        {
            details.Add(new MailDetail("Payment reference", payment.BillId));
        }

        var layout = new MailLayout(
            "Your reservation receipt",
            ["Thank you for reserving with ApexAuto. This receipt records your original confirmation."],
            details,
            ["Check your account for the current reservation and refund status. The deposit is refundable on cancellation or expiry."],
            payment is null ? null : "View Billplz receipt",
            payment?.BillUrl);
        return new OutboundMail(reservation.Buyer.Email, "ApexAuto reservation receipt",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
