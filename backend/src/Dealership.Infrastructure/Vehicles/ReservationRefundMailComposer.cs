using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationRefundMailComposer
{
    public static OutboundMail Completed(ReservationRefund refund, Vehicle vehicle, string recipientEmail)
    {
        var bank = PayoutBanks.All.FirstOrDefault(item => item.Code == refund.BankCode).Name ?? refund.BankCode;
        var layout = new MailLayout(
            "Your deposit refund",
            ["Your reservation deposit has been refunded to your bank account."],
            [
                new MailDetail("Vehicle", MailFormat.VehicleLabel(vehicle)),
                new MailDetail("Refunded", MailFormat.Money(refund.AmountSen / 100m)),
                new MailDetail("Paid to", $"{bank}, account ending {refund.AccountLast4}"),
                new MailDetail("Completed", MailFormat.LocalTime(refund.SettledAt!.Value))
            ],
            ["Your bank may take a short while to show the refund in your account."]);
        return new OutboundMail(recipientEmail, "Your ApexAuto deposit refund",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
