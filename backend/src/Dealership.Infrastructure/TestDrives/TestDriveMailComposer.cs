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

        return new(message.RecipientEmail, $"ApexAuto test drive {state}", body);
    }
}
