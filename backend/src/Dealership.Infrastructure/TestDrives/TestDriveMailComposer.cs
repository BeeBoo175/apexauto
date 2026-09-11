using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.TestDrives;

public static class TestDriveMailComposer
{
    public static OutboundMail Compose(TestDriveMail message, TestDriveBooking booking, string branchName,
        Vehicle vehicle, string? cancellationReason)
    {
        var cancelled = message.Kind == TestDriveMailKind.Cancellation;
        var state = cancelled ? "cancelled" : "confirmed";
        var details = new List<MailDetail>
        {
            new("Reference", booking.Id.ToString("D")),
            new("Branch", branchName),
            new("Vehicle", MailFormat.VehicleLabel(vehicle)),
            new("Starts", MailFormat.LocalTime(booking.StartsAt))
        };
        if (cancellationReason is not null)
        {
            details.Add(new MailDetail("Reason", cancellationReason));
        }

        var layout = new MailLayout(
            cancelled ? "Your test drive is cancelled" : "Your test drive is confirmed",
            [cancelled
                ? "Your test drive booking has been cancelled. No further action is needed."
                : "We look forward to seeing you at the showroom. Please bring your driving licence."],
            details,
            cancelled ? [] : ["Need to change your time? Cancel from your ApexAuto account and book again."]);
        return new(MailRecipient.Require(message.RecipientEmail), $"ApexAuto test drive {state}", MailTemplate.RenderText(layout),
            MailTemplate.Render(layout));
    }
}
