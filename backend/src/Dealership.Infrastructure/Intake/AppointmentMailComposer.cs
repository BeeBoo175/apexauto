using Dealership.Domain.Common;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Intake;

public static class AppointmentMailComposer
{
    private static readonly TimeZoneInfo Malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);

    public static OutboundMail Confirmation(AppointmentConfirmation message, IntakeAppointment appointment, string branchName)
    {
        var startsAt = TimeZoneInfo.ConvertTime(appointment.StartsAt, Malaysia);
        var body = FormattableString.Invariant($"Your ApexAuto inspection booking is confirmed.\nReference: {appointment.Id:D}\nBranch: {branchName}\nStarts: {startsAt:yyyy-MM-dd HH:mm} (Asia/Kuala_Lumpur)\nRegistration: {appointment.RegistrationPlate}");
        return new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking confirmed", body);
    }

    public static OutboundMail Cancellation(AppointmentCancellationMail message)
    {
        var body = $"Your ApexAuto inspection booking is cancelled.\nReference: {message.Id:D}\nTo book again, repeat the vehicle intake process.";
        return new OutboundMail(message.RecipientEmail, "ApexAuto inspection booking cancelled", body);
    }

    public static OutboundMail CheckIn(AppointmentCheckInMail message, IntakeAppointment appointment)
    {
        var body = $"Your vehicle has been checked in for inspection at ApexAuto.\nReference: {message.Id:D}\nRegistration: {appointment.RegistrationPlate}\nYour vehicle is pending inspection.";
        return new OutboundMail(message.RecipientEmail, "ApexAuto vehicle check-in confirmed", body);
    }
}
