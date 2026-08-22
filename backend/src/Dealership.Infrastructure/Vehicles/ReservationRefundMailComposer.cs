using Dealership.Application.Vehicles;
using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Vehicles;

public static class ReservationRefundMailComposer
{
    public static OutboundMail Completed(ReservationRefund refund, Vehicle vehicle, string recipientEmail)
    {
        var settledAt = TimeZoneInfo.ConvertTime(refund.SettledAt!.Value, DealershipLocale.Malaysia);
        var car = $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})";
        var bank = PayoutBanks.All.FirstOrDefault(item => item.Code == refund.BankCode).Name ?? refund.BankCode;
        var amount = refund.AmountSen / 100m;
        var body = FormattableString.Invariant($"""
            Your ApexAuto deposit refund
            Vehicle: {car}
            Refunded: RM {amount:F2}
            Paid to: {bank}, account ending {refund.AccountLast4}
            Completed: {settledAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)
            Your bank may take a short while to show the refund in your account.
            """);
        var html = MailTemplate.Render(new MailLayout(
            "Your deposit refund",
            ["Your reservation deposit has been refunded to your bank account."],
            [
                new MailDetail("Vehicle", car),
                new MailDetail("Refunded", FormattableString.Invariant($"RM {amount:N2}")),
                new MailDetail("Paid to", $"{bank}, account ending {refund.AccountLast4}"),
                new MailDetail("Completed", $"{settledAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)")
            ],
            ["Your bank may take a short while to show the refund in your account."]));
        return new OutboundMail(recipientEmail, "Your ApexAuto deposit refund", body, html);
    }
}
