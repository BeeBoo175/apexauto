using System.Globalization;
using Dealership.Application.Common;

namespace Dealership.Application.Intake;

public static class StaffIntakeCsv
{
    public static readonly string Header = "\uFEFF" + string.Join(',',
        new[] { "Registration", "Vehicle", "Status", "Customer", "Handler", "Handler eligible", "Inspection start (UTC)", "Branch", "Appointment reference" }
            .Select(CsvCell.Quote)) + "\r\n";

    public static string Row(StaffWorklistItem item)
    {
        var vehicle = $"{item.ManufactureYear} {item.Make} {item.Model} {item.Trim}";
        var eligibility = item.HandlerEligible ? "Yes" : "No";
        if (item.HandlerId is null)
        {
            eligibility = "";
        }
        var values = new[] { item.RegistrationPlate, vehicle, item.Status, item.CustomerName,
            item.HandlerEmail ?? "Unassigned", eligibility,
            item.StartsAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            item.BranchName, item.Id.ToString() };
        return string.Join(',', values.Select(CsvCell.Quote)) + "\r\n";
    }
}
