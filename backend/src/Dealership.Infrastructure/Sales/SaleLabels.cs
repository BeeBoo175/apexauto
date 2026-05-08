using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Sales;

internal static class SaleLabels
{
    public static string TradeIn(IntakeAppointment appointment) =>
        $"{appointment.RegistrationPlate} · {appointment.Valuation.ManufactureYear} {appointment.Valuation.Make} " +
        appointment.Valuation.Model;

    public static string Vehicle(int year, string make, string model, string trim) =>
        $"{year} {make} {model} {trim}".Trim();
}
