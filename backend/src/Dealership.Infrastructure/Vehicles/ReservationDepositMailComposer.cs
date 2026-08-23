using Dealership.Application.Vehicles;
using Dealership.Domain.Common;
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
        var expiresAt = TimeZoneInfo.ConvertTime(request.ExpiresAt, DealershipLocale.Malaysia);
        var car = $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})";
        var body = FormattableString.Invariant($"""
            Confirm your ApexAuto reservation deposit
            Vehicle: {car}
            Branch: {branchName}
            Refundable deposit: RM {Reservation.DepositAmount:F2}
            A member of our team has asked to reserve this car for you. It is held for you until {expiresAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur).
            {(paid ? "Open this link and pay the deposit to reserve it:" : "Open this link and choose Confirm deposit to reserve it:")}

            {link}

            If you did not expect this, ignore this message and the hold will lapse.
            """);
        var html = MailTemplate.Render(new MailLayout(
            "Confirm your reservation deposit",
            ["A member of our team has asked to reserve this car for you. Confirm the deposit to complete your reservation."],
            [
                new MailDetail("Vehicle", car),
                new MailDetail("Branch", branchName),
                new MailDetail("Refundable deposit", paid
                    ? $"RM {Reservation.DepositAmount:N2}, paid securely online"
                    : $"RM {Reservation.DepositAmount:N2}"),
                new MailDetail("Held until", $"{expiresAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)")
            ],
            ["If you did not expect this message, ignore it and the hold will lapse. You will receive your reservation receipt after you confirm."],
            "Confirm deposit",
            link));
        return new OutboundMail(message.RecipientEmail, "Confirm your ApexAuto reservation deposit", body, html);
    }
}
