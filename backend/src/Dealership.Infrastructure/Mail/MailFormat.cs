using System.Globalization;
using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;

namespace Dealership.Infrastructure.Mail;

// The one place that turns amounts, times and vehicles into mail text, so every mail reads the same whatever culture
// the host runs in.
public static class MailFormat
{
    public static string Money(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"RM {amount:N2}");

    public static string LocalTime(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, DealershipLocale.Malaysia);
        return string.Create(CultureInfo.InvariantCulture,
            $"{local:dd MMM yyyy, HH:mm} ({DealershipLocale.MalaysiaTimeZoneId})");
    }

    public static string VehicleLabel(Vehicle vehicle) =>
        $"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} ({vehicle.RegistrationPlate})";
}