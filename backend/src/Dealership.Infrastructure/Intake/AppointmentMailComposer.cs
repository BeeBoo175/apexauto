using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public static class AppointmentMailComposer
{

    public static OutboundMail Confirmation(AppointmentMail message, IntakeAppointment appointment, string branchName)
    {
        var layout = new MailLayout(
            "Your inspection booking is confirmed",
            ["We look forward to inspecting your vehicle. Keep the reference below for your records."],
            [
                new MailDetail("Reference", appointment.Id.ToString("D")),
                new MailDetail("Branch", branchName),
                new MailDetail("Starts", MailFormat.LocalTime(appointment.StartsAt)),
                new MailDetail("Registration", appointment.RegistrationPlate)
            ],
            ["Need to change plans? Cancel from your ApexAuto account or repeat the vehicle intake process to book again."]);
        return new OutboundMail(MailRecipient.Require(message.RecipientEmail), "ApexAuto inspection booking confirmed",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }

    public static OutboundMail Cancellation(AppointmentMail message)
    {
        var layout = new MailLayout(
            "Your inspection booking is cancelled",
            ["Your inspection booking has been cancelled. No further action is needed for this booking."],
            [new MailDetail("Reference", message.AppointmentId.ToString("D"))],
            ["To book again, repeat the vehicle intake process."]);
        return new OutboundMail(MailRecipient.Require(message.RecipientEmail), "ApexAuto inspection booking cancelled",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }

    public static OutboundMail CheckIn(AppointmentMail message, IntakeAppointment appointment)
    {
        var layout = new MailLayout(
            "Your vehicle is checked in",
            ["Your vehicle has arrived and is now waiting for its ApexAuto inspection."],
            [
                new MailDetail("Reference", message.AppointmentId.ToString("D")),
                new MailDetail("Registration", appointment.RegistrationPlate),
                new MailDetail("Status", "Pending inspection")
            ],
            []);
        return new OutboundMail(MailRecipient.Require(message.RecipientEmail), "ApexAuto vehicle check-in confirmed",
            MailTemplate.RenderText(layout), MailTemplate.Render(layout));
    }
}
