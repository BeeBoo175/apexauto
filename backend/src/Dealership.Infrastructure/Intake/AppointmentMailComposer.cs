using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public static class AppointmentMailComposer
{

    public static OutboundMail Confirmation(AppointmentMail message, IntakeAppointment appointment, string branchName)
    {
        var startsAt = TimeZoneInfo.ConvertTime(appointment.StartsAt, DealershipLocale.Malaysia);
        var body = FormattableString.Invariant($"Your ApexAuto inspection booking is confirmed.\nReference: {appointment.Id:D}\nBranch: {branchName}\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)\nRegistration: {appointment.RegistrationPlate}");
        var html = MailTemplate.Render(new MailLayout(
            "Your inspection booking is confirmed",
            ["We look forward to inspecting your vehicle. Keep the reference below for your records."],
            [
                new MailDetail("Reference", appointment.Id.ToString("D")),
                new MailDetail("Branch", branchName),
                new MailDetail("Starts", $"{startsAt:dd MMM yyyy, HH:mm} (Asia/Kuala_Lumpur)"),
                new MailDetail("Registration", appointment.RegistrationPlate)
            ],
            ["Need to change plans? Cancel from your ApexAuto account or repeat the vehicle intake process to book again."]));
        return new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking confirmed", body, html);
    }

    public static OutboundMail Cancellation(AppointmentMail message)
    {
        var body = $"Your ApexAuto inspection booking is cancelled.\nReference: {message.AppointmentId:D}\nTo book again, repeat the vehicle intake process.";
        var html = MailTemplate.Render(new MailLayout(
            "Your inspection booking is cancelled",
            ["Your inspection booking has been cancelled. No further action is needed for this booking."],
            [new MailDetail("Reference", message.AppointmentId.ToString("D"))],
            ["To book again, repeat the vehicle intake process."]));
        return new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking cancelled", body, html);
    }

    public static OutboundMail CheckIn(AppointmentMail message, IntakeAppointment appointment)
    {
        var body = $"Your vehicle has been checked in for inspection at ApexAuto.\nReference: {message.AppointmentId:D}\nRegistration: {appointment.RegistrationPlate}\nYour vehicle is pending inspection.";
        var html = MailTemplate.Render(new MailLayout(
            "Your vehicle is checked in",
            ["Your vehicle has arrived and is now waiting for its ApexAuto inspection."],
            [
                new MailDetail("Reference", message.AppointmentId.ToString("D")),
                new MailDetail("Registration", appointment.RegistrationPlate),
                new MailDetail("Status", "Pending inspection")
            ],
            []));
        return new OutboundMail(message.RecipientEmail, "ApexAuto vehicle check-in confirmed", body, html);
    }
}
