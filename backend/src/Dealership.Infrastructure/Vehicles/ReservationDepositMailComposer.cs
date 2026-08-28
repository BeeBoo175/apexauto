using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

// The deposit request: a button that opens the hosted deposit payment, or, while the gateway is off, confirms the
// simulated deposit and makes the reservation.
public sealed class ReservationDepositMailComposer(AuthMailLinks links, ReservationDepositToken tokens,
    IPaymentGateway payment)
{
    public OutboundMail Compose(ReservationDepositMail message, ReservationDepositRequest request, string branchName,
        Vehicle vehicle)
    {
        var link = links.Create("/confirm-deposit", tokens.Create(request));
        var paid = payment.IsEnabled;
        var layout = new MailLayout(
            "Confirm your reservation deposit",
            ["A member of our team has asked to reserve this car for you. Confirm the deposit to complete your reservation."],
            [
                new MailDetail("Vehicle", MailFormat.VehicleLabel(vehicle)),
                new MailDetail("Branch", branchName),
                new MailDetail("Refundable deposit", paid
                    ? $"{MailFormat.Money(Reservation.DepositAmount)}, paid securely online"
                    : MailFormat.Money(Reservation.DepositAmount)),
                new MailDetail("Held until", MailFormat.LocalTime(request.ExpiresAt))
            ],
            ["If you did not expect this message, ignore it and the hold will lapse. You will receive your reservation receipt after you confirm."],
            "Confirm deposit",
            link);
        return new OutboundMail(message.RecipientEmail, "Confirm your ApexAuto reservation deposit",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
