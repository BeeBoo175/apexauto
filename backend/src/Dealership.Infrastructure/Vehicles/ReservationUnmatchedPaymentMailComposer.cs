using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationUnmatchedPaymentMailComposer
{
    public static OutboundMail Compose(ReservationDepositPayment payment, Vehicle vehicle, string recipientEmail,
        string accountUrl)
    {
        var layout = new MailLayout(
            "Your deposit could not reserve the car",
            ["We received your deposit, but the car could not be reserved for you. Your deposit is refundable in full."],
            [
                new MailDetail("Vehicle", MailFormat.VehicleLabel(vehicle)),
                new MailDetail("Paid", MailFormat.Money(payment.PaidAmountSen!.Value / 100m)),
                new MailDetail("Received", MailFormat.LocalTime(payment.PaidAt!.Value)),
                new MailDetail("Payment reference", payment.BillId)
            ],
            ["Open your account and give the bank account to refund to. The refund is sent only when you ask for it."],
            "Claim your refund",
            accountUrl);
        return new OutboundMail(recipientEmail, "Your ApexAuto deposit refund is waiting",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
