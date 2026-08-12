using Dealership.Domain.Common;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.TestDrives;

public static class TestDriveMailComposer
{
    public static OutboundMail Compose(TestDriveMail message, TestDriveBooking booking, string branchName,
        Vehicle vehicle, string? cancellationReason)
    {
        var startsAt = TimeZoneInfo.ConvertTime(booking.StartsAt, DealershipLocale.Malaysia);
        var state = message.Kind == TestDriveMailKind.Cancellation ? "cancelled" : "confirmed";
        var body = FormattableString.Invariant($"Your ApexAuto test drive is {state}.\nReference: {booking.Id:D}\nBranch: {branchName}\nVehicle: {vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)");
        if (cancellationReason is not null)
        {
            body += $"\nReason: {cancellationReason}";
        }

        var cancelled = message.Kind == TestDriveMailKind.Cancellation;
        var details = new List<MailDetail>
        {
            new("Reference", booking.Id.ToString("D")),
            new("Branch", branchName),
            new("Vehicle", $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})"),
            new("Starts", $"{startsAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)")
        };
        if (cancellationReason is not null)
        {
            details.Add(new MailDetail("Reason", cancellationReason));
        }

        var html = MailTemplate.Render(new MailLayout(
            cancelled ? "Your test drive is cancelled" : "Your test drive is confirmed",
            [cancelled
                ? "Your test drive booking has been cancelled. No further action is needed."
                : "We look forward to seeing you at the showroom. Please bring your driving licence."],
            details,
            cancelled ? [] : ["Need to change your time? Cancel from your ApexAuto account and book again."]));
        return new(message.RecipientEmail, $"ApexAuto test drive {state}", body, html);
    }
}
